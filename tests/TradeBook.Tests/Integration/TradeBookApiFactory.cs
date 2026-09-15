using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Boots the real API in-process with its connection string pointed at
/// whatever SQL Server the test supplies. Everything else runs exactly as it
/// does in Program.cs, unless a test adds or replaces services through
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

        if (configureTestServices is not null)
        {
            // Runs after Program.cs has registered everything, so a test can
            // add a service (an EF Core interceptor) or replace one.
            builder.ConfigureTestServices(configureTestServices);
        }
    }
}
