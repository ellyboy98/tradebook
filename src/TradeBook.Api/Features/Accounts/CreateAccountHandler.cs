using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Infrastructure.Time;
using TradeBook.Api.Persistence;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.Accounts;

public sealed class CreateAccountHandler(TradeBookDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<CreateResult<AccountResponse>> HandleAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await dbContext.Accounts.AnyAsync(a => a.Code == code, cancellationToken))
        {
            return new CreateResult<AccountResponse>.Conflict($"An account with code {code} already exists.");
        }

        var account = new Account
        {
            Code = code,
            Name = request.Name.Trim(),
            BaseCurrency = request.BaseCurrency,
            OwnerSubject = request.OwnerSubject.Trim(),
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNowToMilliseconds(),
        };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateResult<AccountResponse>.Created(AccountResponse.From(account));
    }
}
