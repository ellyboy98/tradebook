using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Features.Instruments;

/// <summary>Active instruments with their latest quote, ordered by symbol (design.md section 7).</summary>
public sealed class ListInstrumentsHandler(TradeBookDbContext dbContext)
{
    public async Task<IReadOnlyList<InstrumentResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Instruments
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Symbol)
            .Select(i => new InstrumentResponse(
                i.Id,
                i.Symbol,
                i.Name,
                i.InstrumentType,
                i.Currency,
                i.TickSize,
                i.LotSize,
                i.IsActive,
                // Left join by hand; at most one price row per instrument.
                dbContext.InstrumentPrices.Where(p => p.InstrumentId == i.Id).Select(p => (decimal?)p.LastPrice).FirstOrDefault(),
                dbContext.InstrumentPrices.Where(p => p.InstrumentId == i.Id).Select(p => (DateTime?)p.AsOfUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
