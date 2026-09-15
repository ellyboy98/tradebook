namespace TradeBook.Api.Infrastructure.Auth;

public enum AccessDecision
{
    Allowed,

    /// <summary>403. The caller may not use this account and is told nothing else.</summary>
    Forbidden,

    /// <summary>404. Only reachable by callers entitled to know the account does not exist.</summary>
    NotFound,
}
