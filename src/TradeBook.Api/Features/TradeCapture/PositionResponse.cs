using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// The position figures a trade changes: the "position" object in the
/// <c>POST /api/trades</c> response and, later, the payload of the SignalR
/// <c>PositionUpdated</c> message. Valuation (last price, unrealised) is a
/// separate, richer shape owned by the positions query in build step 6.
/// </summary>
public sealed record PositionResponse(
    int AccountId,
    int InstrumentId,
    decimal NetQuantity,
    decimal AverageCost,
    decimal RealisedPnl)
{
    public static PositionResponse From(Position position) => new(
        position.AccountId,
        position.InstrumentId,
        position.NetQuantity,
        position.AverageCost,
        position.RealisedPnl);
}
