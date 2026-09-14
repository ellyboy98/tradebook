using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeBook.Api.Infrastructure.Health;

public static class HealthCheckExtensions
{
    private const string ConnectionStringName = "TradeBook";

    public static IServiceCollection AddTradeBookHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                name: "sqlserver",
                // The connection string is resolved when the check runs, not
                // when it is registered. Reading configuration eagerly in
                // Program.cs looks tidier but breaks the integration tests:
                // WebApplicationFactory applies its overrides during
                // builder.Build(), after Program.cs has already read the value.
                factory: serviceProvider => new SqlServerHealthCheck(GetConnectionString(serviceProvider)),
                failureStatus: HealthStatus.Unhealthy,
                tags: null,
                // Bound the probe. Without this a hung SQL Server makes the
                // health endpoint hang for the full connection timeout.
                timeout: TimeSpan.FromSeconds(5)));

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

    private static string GetConnectionString(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        return configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
    }
}
