using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Persistence;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// The write path (design.md section 3, part B). Reads are done once; the
/// read-modify-write on the position is wrapped in the optimistic concurrency
/// retry described in design.md section 6.
/// </summary>
public sealed class CaptureTradeHandler(
    TradeBookDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<CaptureTradeHandler> logger)
{
    /// <summary>Design.md section 6: three attempts, then 409.</summary>
    private const int MaxAttempts = 3;

    public async Task<CaptureTradeResult> HandleAsync(
        CaptureTradeRequest request,
        string capturedBySubject,
        CancellationToken cancellationToken)
    {
        // Idempotency first. A repeat of an external reference returns what
        // was booked the first time, whatever else is in the body.
        if (request.ExternalRef is not null)
        {
            var existing = await dbContext.Trades
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.ExternalRef == request.ExternalRef, cancellationToken);

            if (existing is not null)
            {
                // Every trade updates its position in the same commit, so the
                // position must exist; Single (not SingleOrDefault) states that.
                var existingPosition = await dbContext.Positions
                    .AsNoTracking()
                    .SingleAsync(
                        p => p.AccountId == existing.AccountId && p.InstrumentId == existing.InstrumentId,
                        cancellationToken);

                return new CaptureTradeResult.AlreadyCaptured(
                    new CaptureTradeResponse(TradeResponse.From(existing), PositionResponse.From(existingPosition)));
            }
        }

        // References. Unknown ids are 404; they are not payload mistakes.
        // Build step 7 adds the ownership check here, between existence and
        // validation, so a caller learns nothing about accounts they do not own.
        var account = await dbContext.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account is null)
        {
            return new CaptureTradeResult.NotFound("account", request.AccountId);
        }

        var instrument = await dbContext.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == request.InstrumentId, cancellationToken);
        if (instrument is null)
        {
            return new CaptureTradeResult.NotFound("instrument", request.InstrumentId);
        }

        // Payload rules (design.md section 3, B.4). All of them are checked so
        // the caller sees every problem at once, each under its field name.
        var now = timeProvider.GetUtcNow();
        var errors = Validate(request, account, instrument, now);
        if (errors.Count > 0)
        {
            return new CaptureTradeResult.Invalid(errors);
        }

        // Load the position, tracked, because it is about to change. On the
        // first trade for this account and instrument there is none yet.
        var position = await dbContext.Positions
            .SingleOrDefaultAsync(
                p => p.AccountId == request.AccountId && p.InstrumentId == request.InstrumentId,
                cancellationToken);

        if (position is null)
        {
            position = new Position { AccountId = request.AccountId, InstrumentId = request.InstrumentId };
            dbContext.Positions.Add(position);
        }

        var trade = new Trade
        {
            AccountId = request.AccountId,
            InstrumentId = request.InstrumentId,
            Side = request.Side,
            Quantity = request.Quantity,
            Price = request.Price,
            ExecutedAtUtc = request.ExecutedAtUtc.UtcDateTime,
            ExternalRef = request.ExternalRef,
            CapturedBySubject = capturedBySubject,
            CapturedAtUtc = now.UtcDateTime,
        };
        // Added once, outside the loop. If a save attempt fails its transaction
        // rolls back, nothing is inserted, and this entity stays tracked as
        // Added, ready for the next attempt. Adding it again would book twice.
        dbContext.Trades.Add(trade);

        // The state the arithmetic starts from. On a retry it is replaced by
        // whatever the other request left in the database.
        var before = new PositionState(position.NetQuantity, position.AverageCost, position.RealisedPnl);

        for (var attempt = 1; ; attempt++)
        {
            // The arithmetic. This is the only place in the application that
            // produces net quantity, average cost or realised P&L. It is a pure
            // function of (before, trade), which is what makes retrying safe:
            // reapplying it to a fresher `before` gives the right answer.
            var after = PositionMath.Apply(before, request.Side, request.Quantity, request.Price);

            position.NetQuantity = after.NetQuantity;
            position.AverageCost = after.AverageCost;
            position.RealisedPnl = after.RealisedPnl;
            // Setting the navigation, not the id: the trade has no id until it
            // is inserted. EF Core orders the statements so the insert runs
            // first and the generated id lands in last_trade_id.
            position.LastTrade = trade;
            position.UpdatedAtUtc = now.UtcDateTime;

            try
            {
                // One SaveChanges is one database transaction. The trade insert
                // and the position update either both commit or neither does
                // (design.md section 3 step 7). The UPDATE carries the
                // row_version we loaded in its WHERE clause; if another request
                // has committed since, zero rows match and EF Core throws.
                await dbContext.SaveChangesAsync(cancellationToken);

                return new CaptureTradeResult.Captured(
                    new CaptureTradeResponse(TradeResponse.From(trade), PositionResponse.From(position)));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt >= MaxAttempts)
                {
                    logger.LogWarning(
                        ex,
                        "Position for account {AccountId} instrument {InstrumentId} changed on all {MaxAttempts} attempts; returning 409",
                        request.AccountId,
                        request.InstrumentId,
                        MaxAttempts);

                    return new CaptureTradeResult.Conflict();
                }

                logger.LogInformation(
                    "Position for account {AccountId} instrument {InstrumentId} changed concurrently; retrying (attempt {Attempt} of {MaxAttempts})",
                    request.AccountId,
                    request.InstrumentId,
                    attempt + 1,
                    MaxAttempts);

                before = await ReloadPositionAsync(position, cancellationToken)
                    // The row is gone. Nothing in TradeBook deletes positions, so
                    // this cannot happen in practice; if it did, retrying would
                    // not help.
                    ?? throw new InvalidOperationException(
                        $"Position for account {request.AccountId} instrument {request.InstrumentId} disappeared during capture.");
            }
        }
    }

    /// <summary>
    /// Refreshes what EF Core believes the database row looked like when we
    /// loaded it, and returns the position state the next attempt must start
    /// from.
    /// </summary>
    /// <remarks>
    /// This deliberately uses <c>OriginalValues.SetValues</c>, the pattern from
    /// the EF Core concurrency documentation, rather than <c>ReloadAsync</c>.
    /// The original values are what the next UPDATE puts in its WHERE clause,
    /// so they must carry the row_version that is in the database now.
    /// <c>ReloadAsync</c> would also overwrite the <em>current</em> values,
    /// including <c>last_trade_id</c>, with the other request's trade id, and
    /// the link to the trade this request is inserting would be lost.
    /// </remarks>
    private async Task<PositionState?> ReloadPositionAsync(Position position, CancellationToken cancellationToken)
    {
        var entry = dbContext.Entry(position);
        var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
        if (databaseValues is null)
        {
            return null;
        }

        entry.OriginalValues.SetValues(databaseValues);

        return new PositionState(
            databaseValues.GetValue<decimal>(nameof(Position.NetQuantity)),
            databaseValues.GetValue<decimal>(nameof(Position.AverageCost)),
            databaseValues.GetValue<decimal>(nameof(Position.RealisedPnl)));
    }

    private static Dictionary<string, string[]> Validate(
        CaptureTradeRequest request,
        Account account,
        Instrument instrument,
        DateTimeOffset now)
    {
        // Keys are the JSON property names so they line up with what the
        // caller sent, and the wording follows docs/ui-design.md section 7.
        var errors = new Dictionary<string, string[]>();

        if (!account.IsActive)
        {
            errors["accountId"] = ["This account is not active."];
        }

        if (!instrument.IsActive)
        {
            errors["instrumentId"] = ["This instrument is no longer tradeable."];
        }

        if (request.Quantity <= 0m)
        {
            errors["quantity"] = ["Quantity must be greater than zero."];
        }

        if (request.Price <= 0m)
        {
            errors["price"] = ["Price must be greater than zero."];
        }

        if (request.ExecutedAtUtc > now)
        {
            errors["executedAtUtc"] = ["Execution time must not be in the future."];
        }

        return errors;
    }
}
