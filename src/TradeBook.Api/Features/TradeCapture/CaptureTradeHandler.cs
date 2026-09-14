using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Persistence;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// The write path (design.md section 3, part B), without concurrency handling.
/// Build step 5 wraps the body of <see cref="HandleAsync"/> in the retry loop
/// that catches <c>DbUpdateConcurrencyException</c>, reloads the position and
/// reapplies the arithmetic.
/// </summary>
public sealed class CaptureTradeHandler(TradeBookDbContext dbContext, TimeProvider timeProvider)
{
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

        // The arithmetic. This is the only place in the application that
        // produces net quantity, average cost or realised P&L.
        var before = new PositionState(position.NetQuantity, position.AverageCost, position.RealisedPnl);
        var after = PositionMath.Apply(before, request.Side, request.Quantity, request.Price);

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
        dbContext.Trades.Add(trade);

        position.NetQuantity = after.NetQuantity;
        position.AverageCost = after.AverageCost;
        position.RealisedPnl = after.RealisedPnl;
        // Setting the navigation, not the id: the trade has no id until it is
        // inserted. EF Core orders the statements so the insert runs first and
        // the generated id lands in last_trade_id.
        position.LastTrade = trade;
        position.UpdatedAtUtc = now.UtcDateTime;

        // One SaveChanges is one database transaction. The trade insert and
        // the position update either both commit or neither does, which is
        // the guarantee design.md section 3 step 7 asks for. No explicit
        // BeginTransaction is needed because there is a single round of writes.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CaptureTradeResult.Captured(
            new CaptureTradeResponse(TradeResponse.From(trade), PositionResponse.From(position)));
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
