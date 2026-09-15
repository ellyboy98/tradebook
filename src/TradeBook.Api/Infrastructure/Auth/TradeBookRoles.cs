namespace TradeBook.Api.Infrastructure.Auth;

/// <summary>
/// Realm role names exactly as defined in keycloak/realm-export.json and as
/// they arrive in the flattened <c>roles</c> claim.
/// </summary>
public static class TradeBookRoles
{
    public const string Trader = "trader";

    public const string Ops = "ops";
}
