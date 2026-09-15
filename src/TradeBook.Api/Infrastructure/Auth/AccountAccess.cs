using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Infrastructure.Auth;

/// <summary>
/// The ownership rule from design.md section 2, in one place: a trader may
/// only use accounts whose <c>owner_subject</c> is their own subject;
/// operations may use any account. Pure, so it has unit tests.
/// </summary>
public static class AccountAccess
{
    /// <param name="caller">Who is asking.</param>
    /// <param name="account">The account they asked for, or null if no such row exists.</param>
    public static AccessDecision Decide(Caller caller, Account? account)
    {
        if (caller.IsOps)
        {
            return account is null ? AccessDecision.NotFound : AccessDecision.Allowed;
        }

        // A trader gets the same answer for "not yours" and "does not exist".
        // Answering 404 for one and 403 for the other would let anyone probe
        // which account ids are real (design.md section 3, step B.3).
        return account is not null && account.OwnerSubject == caller.Subject
            ? AccessDecision.Allowed
            : AccessDecision.Forbidden;
    }
}
