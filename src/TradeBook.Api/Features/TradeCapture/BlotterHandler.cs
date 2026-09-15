using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// The blotter: an account's executions, filtered and paged. Read-only, so
/// every query is <c>AsNoTracking</c>.
/// </summary>
public sealed class BlotterHandler(TradeBookDbContext dbContext)
{
    /// <returns>The page, or <c>null</c> when the account does not exist.</returns>
    public async Task<BlotterPage?> HandleAsync(BlotterQuery query, CancellationToken cancellationToken)
    {
        // [Required] on the query model guarantees this by the time MVC calls
        // the controller; the throw documents the assumption for other callers.
        var accountId = query.AccountId
            ?? throw new ArgumentException("AccountId is required.", nameof(query));

        // Build step 7 adds the ownership check here.
        if (!await dbContext.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return null;
        }

        var trades = dbContext.Trades
            .AsNoTracking()
            .Where(t => t.AccountId == accountId);

        if (query.InstrumentId is { } instrumentId)
        {
            trades = trades.Where(t => t.InstrumentId == instrumentId);
        }

        // Half-open interval [from, to): a trade exactly at the boundary belongs
        // to the window that starts there, never to two windows.
        if (query.FromUtc is { } fromUtc)
        {
            var from = fromUtc.UtcDateTime;
            trades = trades.Where(t => t.ExecutedAtUtc >= from);
        }

        if (query.ToUtc is { } toUtc)
        {
            var to = toUtc.UtcDateTime;
            trades = trades.Where(t => t.ExecutedAtUtc < to);
        }

        var totalCount = await trades.CountAsync(cancellationToken);

        var items = await trades
            // Id as tie-breaker: two executions with the same timestamp would
            // otherwise have no stable order and could move between pages.
            .OrderByDescending(t => t.ExecutedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            // Projected in the query, so only these columns travel over the wire.
            .Select(t => new TradeResponse(
                t.Id,
                t.AccountId,
                t.InstrumentId,
                t.Side,
                t.Quantity,
                t.Price,
                t.ExecutedAtUtc,
                t.ExternalRef))
            .ToListAsync(cancellationToken);

        return new BlotterPage(items, query.Page, query.PageSize, totalCount);
    }
}
