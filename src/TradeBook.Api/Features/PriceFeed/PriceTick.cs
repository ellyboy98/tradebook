using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Features.Positions;
using TradeBook.Api.Infrastructure.Time;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Features.PriceFeed;

/// <summary>
/// One tick of the push path (design.md section 3, part C): new prices for
/// active instruments, stored; every open position revalued and pushed to
/// its account's group. Scoped, so each tick gets a fresh DbContext, and
/// separate from the hosted service so a test can run exactly one tick.
/// </summary>
public sealed class PriceTick(
    TradeBookDbContext dbContext,
    IPriceGenerator generator,
    IPositionNotifier notifier,
    TimeProvider timeProvider,
    ILogger<PriceTick> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNowToMilliseconds();

        // Tracked on purpose: the rows are updated in place and saved as one batch.
        var prices = await dbContext.InstrumentPrices
            .ToDictionaryAsync(p => p.InstrumentId, cancellationToken);

        var activeInstruments = await dbContext.Instruments
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Select(i => new { i.Id, i.Symbol, i.TickSize })
            .ToListAsync(cancellationToken);

        foreach (var instrument in activeInstruments)
        {
            if (!prices.TryGetValue(instrument.Id, out var price))
            {
                // A walk needs a starting point. Instruments are seeded with one;
                // an instrument created without a price is simply not quoted.
                logger.LogDebug("No starting price for {Symbol}; not quoted", instrument.Symbol);
                continue;
            }

            price.LastPrice = generator.NextPrice(price.LastPrice, instrument.TickSize);
            price.AsOfUtc = now;
        }

        // Stored before pushing so a restart values positions from the same
        // snapshot the browser last saw (design.md section 4).
        await dbContext.SaveChangesAsync(cancellationToken);

        // Open positions only; a flat position has nothing to mark.
        var openPositions = await dbContext.Positions
            .AsNoTracking()
            .Where(p => p.NetQuantity != 0m)
            .Select(p => new { p.AccountId, p.InstrumentId, p.NetQuantity, p.AverageCost, p.RealisedPnl })
            .ToListAsync(cancellationToken);

        foreach (var position in openPositions)
        {
            if (!prices.TryGetValue(position.InstrumentId, out var price))
            {
                continue;
            }

            var state = new PositionState(position.NetQuantity, position.AverageCost, position.RealisedPnl);
            // Same rounding as GET /api/accounts/{id}/positions, so the pushed
            // figure and the fetched figure agree to the digit.
            var unrealised = Math.Round(PositionMath.UnrealisedPnl(state, price.LastPrice), 6, MidpointRounding.ToEven);

            await notifier.PositionValuedAsync(
                new PositionValuedMessage(position.AccountId, position.InstrumentId, price.LastPrice, unrealised, now),
                cancellationToken);
        }
    }
}
