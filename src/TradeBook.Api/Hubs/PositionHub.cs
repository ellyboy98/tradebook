using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Hubs;

/// <summary>
/// Live positions, at <c>/hubs/positions</c>. Clients subscribe per account
/// and receive <c>PositionUpdated</c> and <c>PositionValued</c> for it.
/// Authorisation is enforced here, not only at the REST layer: a client may
/// only join the group of an account it could read over REST.
/// </summary>
/// <remarks>
/// The hub is constructed per method invocation while the connection can
/// live for hours, so nothing scoped is held on the instance. Each method
/// creates a DbContext from the factory and disposes it before returning.
/// </remarks>
[Authorize(Policy = Policies.TraderOrOps)]
public sealed class PositionHub(IDbContextFactory<TradeBookDbContext> dbContextFactory) : Hub<IPositionClient>
{
    public static string GroupName(int accountId) => $"account-{accountId}";

    public async Task SubscribeAccount(int accountId)
    {
        var caller = Caller.From(Context.User ?? throw new HubException("Not authenticated."));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(Context.ConnectionAborted);
        var account = await dbContext.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == accountId, Context.ConnectionAborted);

        // HubException is the one exception type whose message SignalR sends
        // to the client; anything else is replaced by a generic error.
        switch (AccountAccess.Decide(caller, account))
        {
            case AccessDecision.Forbidden:
                throw new HubException(AccountAccess.DeniedMessage);
            case AccessDecision.NotFound:
                throw new HubException($"No account with id {accountId} exists.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId), Context.ConnectionAborted);
    }

    public Task UnsubscribeAccount(int accountId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(accountId), Context.ConnectionAborted);
}
