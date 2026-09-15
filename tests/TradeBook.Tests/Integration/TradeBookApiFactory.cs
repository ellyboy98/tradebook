using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeBook.Api.Infrastructure.Auth;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Boots the real API in-process with its connection string pointed at
/// whatever SQL Server the test supplies, and its JWT validation pointed at
/// the local test signing key. Everything else runs exactly as it does in
/// Program.cs, unless a test adds or replaces services through
/// <paramref name="configureTestServices"/>.
/// </summary>
public sealed class TradeBookApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureTestServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added after appsettings.*.json, so it wins. Note that these values
        // only reach code that reads configuration after the host is built;
        // see PersistenceServiceCollectionExtensions for why that matters.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TradeBook"] = connectionString,
                // The fixture owns the schema. Each factory must not race it.
                ["Database:MigrateOnStartup"] = "false",
            }));

        // Runs after Program.cs has registered everything.
        builder.ConfigureTestServices(services =>
        {
            // Swap Keycloak's published keys for the local test key. Runs after
            // the framework's own post-configuration, so nulling the
            // ConfigurationManager stops any metadata download.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null;
                options.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
                options.TokenValidationParameters.IssuerSigningKey = TestTokens.SigningKey;
            });

            configureTestServices?.Invoke(services);
        });
    }

    /// <summary>A client carrying a token for the given subject and roles.</summary>
    public HttpClient CreateClientAs(string subject, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject, roles));
        return client;
    }

    /// <summary>An operations user, who may use any account. The default for tests that are not about authorisation.</summary>
    public HttpClient CreateOpsClient() => CreateClientAs(TestTokens.OpsSubject, TradeBookRoles.Ops);
}
