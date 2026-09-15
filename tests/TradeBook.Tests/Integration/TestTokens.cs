using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Mints JWTs shaped like Keycloak's (sub, preferred_username, a flat
/// <c>roles</c> array, aud = tradebook-api) but signed with a local key that
/// <see cref="TradeBookApiFactory"/> tells the API to trust. Everything else
/// about validation (audience, expiry, role and subject claims) is the real
/// code path; only the key source differs. The real Keycloak flow is proven
/// against the compose stack.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://tests.tradebook.local/realms/test";

    /// <summary>Must match Keycloak:Audience in appsettings.json.</summary>
    public const string Audience = "tradebook-api";

    /// <summary>Subject used by <see cref="TradeBookApiFactory.CreateOpsClient"/>.</summary>
    public const string OpsSubject = "11111111-1111-1111-1111-111111111111";

    // 64 bytes; HMAC-SHA256 needs at least 32. Tests only.
    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("tradebook-integration-tests-signing-key-0123456789abcdef0123456789"));

    public static string Create(string subject, params string[] roles)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["preferred_username"] = $"user-{subject[..8]}",
                // A JSON array, exactly as Keycloak's realm-role mapper emits it.
                ["roles"] = roles,
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
