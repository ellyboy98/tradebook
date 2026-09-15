using System.Security.Claims;

namespace TradeBook.Api.Infrastructure.Auth;

/// <summary>
/// The authenticated caller reduced to what authorisation needs: the Keycloak
/// user id and whether they hold the ops role. Handlers take this rather than
/// a <see cref="ClaimsPrincipal"/> so they stay ignorant of token formats.
/// </summary>
/// <param name="Subject">The <c>sub</c> claim; compared against <c>accounts.owner_subject</c>.</param>
/// <param name="IsOps">Whether the caller may read any account.</param>
public sealed record Caller(string Subject, bool IsOps)
{
    public static Caller From(ClaimsPrincipal user)
    {
        // MapInboundClaims is off in the JWT bearer options, so the claim keeps
        // its JWT name "sub" instead of being renamed to nameidentifier.
        var subject = user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The authenticated principal has no sub claim.");

        // IsInRole honours the RoleClaimType configured for the token ("roles").
        return new Caller(subject, user.IsInRole(TradeBookRoles.Ops));
    }
}
