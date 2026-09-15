using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TradeBook.Api.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "TradeBook";

    public static IServiceCollection AddTradeBookPersistence(this IServiceCollection services)
    {
        // The (serviceProvider, options) overload runs when DbContextOptions is
        // first resolved, after the host is built. Reading configuration here
        // rather than in Program.cs means test overrides applied by
        // WebApplicationFactory are visible.
        //
        // Two ways to get a context are registered:
        //  - TradeBookDbContext itself, scoped: one per HTTP request. Handlers
        //    take this.
        //  - IDbContextFactory<TradeBookDbContext>, singleton: for code that
        //    is not scoped to a request, such as the SignalR hub. The hosted
        //    price feed instead creates a scope per tick (see PriceFeedService).
        // The factory is a singleton, so the options it builds contexts from
        // must be singleton too; that is what the optionsLifetime argument does.
        services.AddDbContext<TradeBookDbContext>(
            (serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

                options.UseSqlServer(connectionString);

                // Any IInterceptor registered in DI is attached to every DbContext.
                // Production registers none, so this is a no-op there. The
                // integration tests register one to control the exact moment two
                // requests read the same position, which is what makes the
                // concurrency tests deterministic instead of a race against luck.
                options.AddInterceptors(serviceProvider.GetServices<IInterceptor>());
            },
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddDbContextFactory<TradeBookDbContext>();

        return services;
    }
}
