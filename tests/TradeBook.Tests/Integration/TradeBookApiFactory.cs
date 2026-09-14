using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Boots the real API in-process with its connection string pointed at
/// whatever SQL Server the test supplies. Everything else runs exactly as it
/// does in Program.cs.
/// </summary>
public sealed class TradeBookApiFactory(string connectionString) : WebApplicationFactory<Program>
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
    }
}
