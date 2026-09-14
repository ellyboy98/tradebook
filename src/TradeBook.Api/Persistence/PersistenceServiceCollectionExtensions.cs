using Microsoft.EntityFrameworkCore;

namespace TradeBook.Api.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "TradeBook";

    public static IServiceCollection AddTradeBookPersistence(this IServiceCollection services)
    {
        // The (serviceProvider, options) overload runs when DbContextOptions is
        // first resolved, after the host is built. Reading configuration here
        // rather than in Program.cs means test overrides applied by
        // WebApplicationFactory are visible. DbContext is scoped: one per HTTP
        // request. Singletons (the SignalR hub, the hosted price feed) must not
        // inject it directly; see CLAUDE.md.
        services.AddDbContext<TradeBookDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options.UseSqlServer(connectionString);
        });

        return services;
    }
}
