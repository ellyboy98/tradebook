namespace TradeBook.Api.Infrastructure.Auth;

/// <summary>Authorisation policy names used in <c>[Authorize(Policy = …)]</c>.</summary>
public static class Policies
{
    /// <summary>Any signed-in TradeBook user. Ownership is checked separately, per account, in the handlers.</summary>
    public const string TraderOrOps = "TraderOrOps";

    /// <summary>Operations only: seeding instruments and accounts.</summary>
    public const string OpsOnly = "OpsOnly";
}
