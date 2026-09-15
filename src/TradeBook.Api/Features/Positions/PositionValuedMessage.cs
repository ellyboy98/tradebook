namespace TradeBook.Api.Features.Positions;

/// <summary>
/// Payload of the SignalR <c>PositionValued</c> message (design.md section 8):
/// which position, the price it was marked at, the resulting unrealised
/// P&amp;L, and when. Sent once per open position on every price tick.
/// </summary>
public sealed record PositionValuedMessage(
    int AccountId,
    int InstrumentId,
    decimal LastPrice,
    decimal UnrealisedPnl,
    DateTime AsOfUtc);
