using TradeBook.Api.Domain;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>The trade as the API presents it. Projected by hand from the entity (no AutoMapper, CLAUDE.md).</summary>
public sealed record TradeResponse(
    long Id,
    int AccountId,
    int InstrumentId,
    TradeSide Side,
    decimal Quantity,
    decimal Price,
    DateTime ExecutedAtUtc,
    string? ExternalRef)
{
    public static TradeResponse From(Trade trade) => new(
        trade.Id,
        trade.AccountId,
        trade.InstrumentId,
        trade.Side,
        trade.Quantity,
        trade.Price,
        trade.ExecutedAtUtc,
        trade.ExternalRef);
}
