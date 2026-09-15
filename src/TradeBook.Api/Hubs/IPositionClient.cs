using TradeBook.Api.Features.Positions;
using TradeBook.Api.Features.TradeCapture;

namespace TradeBook.Api.Hubs;

/// <summary>
/// The server-to-client half of the hub contract (design.md section 8). A
/// strongly typed hub means a typo in a method name is a compile error, not
/// a message that silently never arrives.
/// </summary>
public interface IPositionClient
{
    /// <summary>A trade changed the position.</summary>
    Task PositionUpdated(PositionResponse position, CancellationToken cancellationToken);

    /// <summary>A price tick revalued an open position.</summary>
    Task PositionValued(PositionValuedMessage valuation, CancellationToken cancellationToken);
}
