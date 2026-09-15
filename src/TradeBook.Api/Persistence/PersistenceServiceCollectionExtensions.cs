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
        // WebApplicationFactory are visible. DbContext is scoped: one per HTTP
        // request. Singletons (the SignalR hub, the hosted price feed) must not
        // inject it directly; see CLAUDE.md.
        services.AddDbContext<TradeBookDbContext>((serviceProvider, options) =>
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
        });

        return services;
    }
}
