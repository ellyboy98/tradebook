using System.Net;

namespace TradeBook.Tests.Integration;

/// <summary>
/// The blotter page and what it needs before sign-in must be reachable
/// anonymously; everything else on the site is behind the fallback policy.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class StaticPageTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TradeBookApiFactory _factory = null!;
    private HttpClient _anonymous = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        _anonymous = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_page_is_served_at_the_root_without_a_token()
    {
        using var response = await _anonymous.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain("TradeBook").And.Contain("lib/signalr/signalr.min.js");
    }

    [Fact]
    public async Task The_signalr_client_script_is_served_from_wwwroot()
    {
        using var response = await _anonymous.GetAsync("/lib/signalr/signalr.min.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Fact]
    public async Task Client_config_is_anonymous_and_names_the_public_client()
    {
        var (response, body) = await _anonymous.GetJsonAsync("/api/client-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("authority").GetString().Should().Be("http://localhost:8080/realms/tradebook");
        body.GetProperty("clientId").GetString().Should().Be("tradebook-web");
        body.GetProperty("hubPath").GetString().Should().Be("/hubs/positions");
    }
}
