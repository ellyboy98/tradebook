using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Features.Positions;

/// <summary>
/// Current positions for an account, valued against the latest price. Flat
/// positions are hidden unless asked for (ADR-012).
/// </summary>
public sealed class GetPositionsHandler(TradeBookDbContext dbContext)
{
    /// <returns>The positions, or <c>null</c> when the account does not exist.</returns>
    public async Task<IReadOnlyList<PositionValuationResponse>?> HandleAsync(
        int accountId,
        bool includeFlat,
        CancellationToken cancellationToken)
    {
        // Build step 7 adds the ownership check here.
        if (!await dbContext.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return null;
        }

        var rows = await dbContext.Positions
            .AsNoTracking()
            .Where(p => p.AccountId == accountId)
            // includeFlat is a parameter in the SQL, so this is one query either way.
            .Where(p => includeFlat || p.NetQuantity != 0m)
            .Select(p => new
            {
                p.AccountId,
                p.InstrumentId,
                p.Instrument.Symbol,
                p.NetQuantity,
                p.AverageCost,
                p.RealisedPnl,
                p.UpdatedAtUtc,
                // Left join by hand: instrument_prices has at most one row per
                // instrument and may have none.
                Price = dbContext.InstrumentPrices
                    .Where(x => x.InstrumentId == p.InstrumentId)
                    .Select(x => new { x.LastPrice, x.AsOfUtc })
                    .FirstOrDefault(),
            })
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        // Valuation happens here, in memory, because the formula belongs to
        // PositionMath and SQL is not where domain rules live.
        return rows
            .Select(row =>
            {
                var state = new PositionState(row.NetQuantity, row.AverageCost, row.RealisedPnl);

                return new PositionValuationResponse(
                    row.AccountId,
                    row.InstrumentId,
                    row.Symbol,
                    row.NetQuantity,
                    row.AverageCost,
                    row.RealisedPnl,
                    LastPrice: row.Price?.LastPrice,
                    // Quantity (4 dp) × price (6 dp) yields 10 dp. PositionMath stays
                    // exact; the response rounds to the six places every other
                    // money figure uses.
                    UnrealisedPnl: row.Price is null
                        ? null
                        : Math.Round(PositionMath.UnrealisedPnl(state, row.Price.LastPrice), 6, MidpointRounding.ToEven),
                    PricedAtUtc: row.Price?.AsOfUtc,
                    row.UpdatedAtUtc);
            })
            .ToList();
    }
}
