namespace TradeBook.Api.Features.TradeCapture;

/// <summary>Body of the 201 (and the 200 idempotent replay) from <c>POST /api/trades</c>.</summary>
public sealed record CaptureTradeResponse(TradeResponse Trade, PositionResponse Position);
