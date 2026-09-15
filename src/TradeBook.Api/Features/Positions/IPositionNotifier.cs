using TradeBook.Api.Features.TradeCapture;

namespace TradeBook.Api.Features.Positions;

/// <summary>
/// Outbound port for pushing position changes to whoever is watching an
/// account. Features depend on this interface; the SignalR implementation
/// lives in <c>Hubs</c>, so neither the trade capture handler nor the price
/// feed knows anything about SignalR.
/// </summary>
public interface IPositionNotifier
{
    /// <summary>A trade changed the position: net quantity, average cost, realised.</summary>
    Task PositionUpdatedAsync(PositionResponse position, CancellationToken cancellationToken);

    /// <summary>A price tick revalued an open position.</summary>
    Task PositionValuedAsync(PositionValuedMessage valuation, CancellationToken cancellationToken);
}
