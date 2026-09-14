using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeBook.Api.Persistence;

namespace TradeBook.Api.Infrastructure.Health;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddTradeBookHealthChecks(this IServiceCollection services)
    {
        // Resolves a scoped TradeBookDbContext on every run and calls
        // CanConnectAsync, which opens a connection to the TradeBook database
        // itself. Unhealthy until migrations have created that database.
        services.AddHealthChecks()
            .AddDbContextCheck<TradeBookDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy);

        return services;
    }

    public static IEndpointRouteBuilder MapTradeBookHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Anonymous by design (design.md section 7). Orchestrators and load
        // balancers have no token. AllowAnonymous is inert until
        // authentication is wired up, but stating it now records the intent.
        endpoints
            .MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = HealthResponseWriter.WriteAsync,
            })
            .AllowAnonymous();

        return endpoints;
    }
}
