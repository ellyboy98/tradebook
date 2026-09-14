using Testcontainers.MsSql;

namespace TradeBook.Tests.Integration;

/// <summary>
/// One SQL Server container shared by every test in the Integration
/// collection. Starting SQL Server takes tens of seconds, so it is paid once
/// per test run rather than once per test class.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same tag as docker-compose.yml so the image is pulled once.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
