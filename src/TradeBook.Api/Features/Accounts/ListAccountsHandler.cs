using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Features.Accounts;

/// <summary>
/// The accounts the caller may use: their own for a trader, all of them for
/// ops. This is the same rule as <see cref="AccountAccess.Decide"/>, expressed
/// as a filter so the page can build its account rail in one call.
/// </summary>
public sealed class ListAccountsHandler(TradeBookDbContext dbContext)
{
    public async Task<IReadOnlyList<AccountResponse>> HandleAsync(Caller caller, CancellationToken cancellationToken)
    {
        var accounts = dbContext.Accounts.AsNoTracking();

        if (!caller.IsOps)
        {
            accounts = accounts.Where(a => a.OwnerSubject == caller.Subject);
        }

        return await accounts
            .OrderBy(a => a.Code)
            .Select(a => new AccountResponse(a.Id, a.Code, a.Name, a.BaseCurrency, a.OwnerSubject, a.IsActive))
            .ToListAsync(cancellationToken);
    }
}
