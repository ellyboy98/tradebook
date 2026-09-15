using System.ComponentModel.DataAnnotations;

namespace TradeBook.Api.Infrastructure.Auth;

/// <summary>
/// The <c>Keycloak</c> configuration section. Validated at startup, so a
/// missing value stops the API from starting rather than failing the first
/// authenticated request.
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Where the API fetches the realm's OpenID Connect metadata and signing
    /// keys. Inside docker compose this is the internal hostname
    /// (<c>http://keycloak:8080/realms/tradebook</c>).
    /// </summary>
    [Required]
    [Url]
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// The <c>iss</c> claim tokens carry: the realm URL as the browser reached
    /// it (<c>http://localhost:8080/realms/tradebook</c>). Inside compose this
    /// differs from <see cref="Authority"/>, which is why both exist.
    /// </summary>
    [Required]
    [Url]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>The <c>aud</c> claim tokens must carry: the <c>tradebook-api</c> client in the realm.</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>False only for local development, where Keycloak runs on plain HTTP.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>The public client the browser page signs in as (keycloak/realm-export.json).</summary>
    [Required]
    public string WebClientId { get; init; } = "tradebook-web";
}
