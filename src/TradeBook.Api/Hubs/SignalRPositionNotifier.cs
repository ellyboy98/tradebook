using Microsoft.AspNetCore.SignalR;
using TradeBook.Api.Features.Positions;
using TradeBook.Api.Features.TradeCapture;

namespace TradeBook.Api.Hubs;

/// <summary>
/// The SignalR implementation of <see cref="IPositionNotifier"/>. Each
/// message goes to the group for its account; a group with no subscribers
/// costs nothing.
/// </summary>
public sealed class SignalRPositionNotifier(IHubContext<PositionHub, IPositionClient> hubContext) : IPositionNotifier
{
    public Task PositionUpdatedAsync(PositionResponse position, CancellationToken cancellationToken)
        => hubContext.Clients
            .Group(PositionHub.GroupName(position.AccountId))
            .PositionUpdated(position, cancellationToken);

    public Task PositionValuedAsync(PositionValuedMessage valuation, CancellationToken cancellationToken)
        => hubContext.Clients
            .Group(PositionHub.GroupName(valuation.AccountId))
            .PositionValued(valuation, cancellationToken);
}
