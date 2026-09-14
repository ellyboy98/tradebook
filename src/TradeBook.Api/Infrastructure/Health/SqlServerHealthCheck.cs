using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeBook.Api.Infrastructure.Health;

/// <summary>
/// Reports whether SQL Server accepts connections and answers a trivial query.
/// </summary>
/// <remarks>
/// The check connects to <c>master</c> rather than the application database
/// on purpose. The application database is created by migrations, so on a
/// fresh environment it may not exist yet; this check answers the narrower
/// question "is the server there and are the credentials right". Schema
/// readiness is a separate concern that arrives with the DbContext.
/// </remarks>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public SqlServerHealthCheck(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQL Server is reachable");
        }
        catch (Exception ex)
        {
            // A health check must never throw; the framework expects a result.
            // The exception is attached so the health check service logs it.
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "SQL Server is unreachable",
                ex);
        }
    }
}
