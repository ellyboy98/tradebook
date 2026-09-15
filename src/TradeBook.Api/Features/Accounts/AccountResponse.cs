using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Features.Accounts;

public sealed record AccountResponse(
    int Id,
    string Code,
    string Name,
    string BaseCurrency,
    string OwnerSubject,
    bool IsActive)
{
    public static AccountResponse From(Account account) => new(
        account.Id,
        account.Code,
        account.Name,
        account.BaseCurrency,
        account.OwnerSubject,
        account.IsActive);
}
