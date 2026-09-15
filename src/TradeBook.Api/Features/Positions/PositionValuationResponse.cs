namespace TradeBook.Api.Features.Positions;

/// <summary>
/// One row of <c>GET /api/accounts/{id}/positions</c>: the stored position
/// plus its mark-to-market against the latest price. Price fields are null
/// when no price has been recorded for the instrument yet.
/// </summary>
public sealed record PositionValuationResponse(
    int AccountId,
    int InstrumentId,
    string Symbol,
    decimal NetQuantity,
    decimal AverageCost,
    decimal RealisedPnl,
    decimal? LastPrice,
    decimal? UnrealisedPnl,
    DateTime? PricedAtUtc,
    DateTime UpdatedAtUtc);
