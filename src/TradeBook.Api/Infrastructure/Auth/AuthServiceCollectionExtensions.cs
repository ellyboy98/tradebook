using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TradeBook.Api.Infrastructure.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddTradeBookAuthentication(this IServiceCollection services)
    {
        services.AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through the options pipeline with KeycloakOptions as a
        // dependency, rather than reading builder.Configuration in Program.cs.
        // It runs when the options are first needed, after the host is built.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((jwt, keycloakOptions) =>
            {
                var keycloak = keycloakOptions.Value;

                // The handler downloads {Authority}/.well-known/openid-configuration
                // once, caches the signing keys, and refreshes them when it sees
                // a token signed with an unknown key id.
                jwt.Authority = keycloak.Authority;
                jwt.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;

                // Keep JWT claim names as they are. The default renames "sub" to
                // the long ClaimTypes.NameIdentifier URI, and the ownership
                // check wants to compare the raw Keycloak user id.
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    // Explicit, because inside compose the metadata is fetched from
                    // http://keycloak:8080 while tokens say http://localhost:8080.
                    ValidIssuer = keycloak.Issuer,
                    ValidAudience = keycloak.Audience,
                    NameClaimType = "preferred_username",
                    // Keycloak nests realm roles under realm_access.roles, which
                    // ASP.NET Core cannot read as roles. The realm export adds a
                    // mapper that also emits them as a flat "roles" claim.
                    RoleClaimType = "roles",
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ReadHubTokenFromQueryString,
                };
            });

        return services;
    }

    public static IServiceCollection AddTradeBookAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Every endpoint requires a signed-in caller unless it opts out with
            // [AllowAnonymous], as /health does. Forgetting an attribute fails
            // closed rather than open.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.TraderOrOps, policy => policy.RequireRole(TradeBookRoles.Trader, TradeBookRoles.Ops))
            .AddPolicy(Policies.OpsOnly, policy => policy.RequireRole(TradeBookRoles.Ops));

        return services;
    }

    /// <summary>
    /// A browser cannot set headers on a WebSocket handshake, so the SignalR
    /// JavaScript client sends the token as <c>?access_token=…</c>. Honoured
    /// only under <c>/hubs</c>; everywhere else the Authorization header is
    /// the only way in.
    /// </summary>
    private static Task ReadHubTokenFromQueryString(MessageReceivedContext context)
    {
        if (context.Request.Path.StartsWithSegments("/hubs")
            && context.Request.Query.TryGetValue("access_token", out var accessToken)
            && !string.IsNullOrEmpty(accessToken))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
