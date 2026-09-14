using System.Net;
using System.Text.Json;

namespace TradeBook.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class HealthEndpointTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Health_returns_200_with_sqlserver_healthy_when_database_is_reachable()
    {
        await using var factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        var sqlCheck = body.RootElement.GetProperty("checks").EnumerateArray()
            .Should().ContainSingle(check => check.GetProperty("name").GetString() == "sqlserver")
            .Subject;
        sqlCheck.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_returns_503_with_sqlserver_unhealthy_when_database_is_unreachable()
    {
        // Port 1 has nothing listening. Short timeouts keep the test fast; the
        // 5 second health check timeout is the upper bound either way.
        const string unreachable =
            "Server=127.0.0.1,1;Database=master;User Id=sa;Password=unused;" +
            "Connect Timeout=2;ConnectRetryCount=0;Encrypt=True;TrustServerCertificate=True";

        await using var factory = new TradeBookApiFactory(unreachable);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
    }
}
