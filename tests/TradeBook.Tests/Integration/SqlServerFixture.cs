using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TradeBook.Api.Persistence;

namespace TradeBook.Tests.Integration;

/// <summary>
/// One SQL Server container shared by every test in the Integration
/// collection. Starting SQL Server takes tens of seconds, so it is paid once
/// per test run rather than once per test class. The schema is created here,
/// once, by running the real migrations.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same tag as docker-compose.yml so the image is pulled once.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>
    /// Points at a dedicated TradeBook database, not master. Testcontainers
    /// hands out a master connection string; migrating into master would work
    /// but proves nothing about creating the real database.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "TradeBook",
        }.ConnectionString;

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// A DbContext for tests that need to inspect or arrange data directly.
    /// The caller disposes it.
    /// </summary>
    public TradeBookDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradeBookDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new TradeBookDbContext(options);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
