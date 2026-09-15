using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Infrastructure.Time;
using TradeBook.Api.Persistence;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.Instruments;

public sealed class CreateInstrumentHandler(TradeBookDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<CreateResult<InstrumentResponse>> HandleAsync(
        CreateInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var symbol = request.Symbol.Trim().ToUpperInvariant();

        // The unique index is the real guard; this check turns the common case
        // into a 409 with a sentence instead of a database error.
        if (await dbContext.Instruments.AnyAsync(i => i.Symbol == symbol, cancellationToken))
        {
            return new CreateResult<InstrumentResponse>.Conflict($"An instrument with symbol {symbol} already exists.");
        }

        var instrument = new Instrument
        {
            Symbol = symbol,
            Name = request.Name.Trim(),
            InstrumentType = InstrumentType.Equity,
            Currency = request.Currency,
            TickSize = request.TickSize,
            LotSize = request.LotSize,
            IsActive = true,
        };
        dbContext.Instruments.Add(instrument);

        DateTime? pricedAt = null;
        if (request.InitialPrice is { } initialPrice)
        {
            pricedAt = timeProvider.GetUtcNowToMilliseconds();
            // Navigation, not id: the instrument has no id until it is inserted.
            dbContext.InstrumentPrices.Add(new InstrumentPrice
            {
                Instrument = instrument,
                LastPrice = initialPrice,
                AsOfUtc = pricedAt.Value,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateResult<InstrumentResponse>.Created(new InstrumentResponse(
            instrument.Id,
            instrument.Symbol,
            instrument.Name,
            instrument.InstrumentType,
            instrument.Currency,
            instrument.TickSize,
            instrument.LotSize,
            instrument.IsActive,
            request.InitialPrice,
            pricedAt));
    }
}
