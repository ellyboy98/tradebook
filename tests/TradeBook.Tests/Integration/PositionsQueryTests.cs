using System.Net;
using System.Text.Json;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>
/// GET /api/accounts/{id}/positions. Seeded prices are AAPL 11.02 and MSFT
/// 8.50 (SeedData); nothing changes them until the price feed exists.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PositionsQueryTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const int Msft = 2; // seeded instrument

    private TradeBookApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        _client = _factory.CreateOpsClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Values_long_and_short_positions_against_the_latest_price()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        await _client.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m));
        await _client.PostTradeAsync(Trade(accountId, "Sell", 200m, 9.00m, instrumentId: Msft));

        var (response, body) = await _client.GetJsonAsync($"/api/accounts/{accountId}/positions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetArrayLength().Should().Be(2);

        var aapl = body[0];
        aapl.GetProperty("symbol").GetString().Should().Be("AAPL", "ordered by symbol");
        aapl.GetProperty("netQuantity").GetDecimal().Should().Be(300m);
        aapl.GetProperty("averageCost").GetDecimal().Should().Be(10.00m);
        aapl.GetProperty("realisedPnl").GetDecimal().Should().Be(0m);
        aapl.GetProperty("lastPrice").GetDecimal().Should().Be(11.02m);
        aapl.GetProperty("unrealisedPnl").GetDecimal().Should().Be(306.00m, "(11.02 − 10.00) × 300");
        aapl.GetProperty("pricedAtUtc").GetString().Should().Be("2026-09-15T00:00:00Z");
        aapl.GetProperty("updatedAtUtc").ValueKind.Should().Be(JsonValueKind.String);

        var msft = body[1];
        msft.GetProperty("symbol").GetString().Should().Be("MSFT");
        msft.GetProperty("netQuantity").GetDecimal().Should().Be(-200m);
        msft.GetProperty("lastPrice").GetDecimal().Should().Be(8.50m);
        msft.GetProperty("unrealisedPnl").GetDecimal().Should().Be(100.00m, "a short gains when the price falls: (8.50 − 9.00) × −200");
    }

    [Fact]
    public async Task Hides_flat_positions_unless_asked()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        // The worked example from design.md section 5 ends flat with −525 realised.
        foreach (var (side, quantity, price) in new[]
        {
            ("Buy", 300m, 10.00m), ("Buy", 200m, 12.00m), ("Sell", 100m, 13.00m),
            ("Sell", 600m, 9.00m), ("Buy", 50m, 8.00m), ("Buy", 150m, 9.50m),
        })
        {
            await _client.PostTradeAsync(Trade(accountId, side, quantity, price));
        }

        var (_, hidden) = await _client.GetJsonAsync($"/api/accounts/{accountId}/positions");
        var (_, shown) = await _client.GetJsonAsync($"/api/accounts/{accountId}/positions?includeFlat=true");

        hidden.GetArrayLength().Should().Be(0);
        shown.GetArrayLength().Should().Be(1);
        shown[0].GetProperty("netQuantity").GetDecimal().Should().Be(0m);
        shown[0].GetProperty("averageCost").GetDecimal().Should().Be(0m);
        shown[0].GetProperty("realisedPnl").GetDecimal().Should().Be(-525m);
        shown[0].GetProperty("unrealisedPnl").GetDecimal().Should().Be(0m, "nothing is open");
    }

    [Fact]
    public async Task Returns_null_valuation_when_the_instrument_has_no_price_yet()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        var unpricedInstrumentId = await sqlServer.CreateInstrumentAsync(isActive: true);
        await _client.PostTradeAsync(Trade(accountId, "Buy", 10m, 5m, instrumentId: unpricedInstrumentId));

        var (_, body) = await _client.GetJsonAsync($"/api/accounts/{accountId}/positions");

        body.GetArrayLength().Should().Be(1);
        body[0].GetProperty("netQuantity").GetDecimal().Should().Be(10m);
        body[0].GetProperty("lastPrice").ValueKind.Should().Be(JsonValueKind.Null);
        body[0].GetProperty("unrealisedPnl").ValueKind.Should().Be(JsonValueKind.Null);
        body[0].GetProperty("pricedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Returns_an_empty_list_for_an_account_with_no_positions()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.GetJsonAsync($"/api/accounts/{accountId}/positions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_account()
    {
        var (response, body) = await _client.GetJsonAsync($"/api/accounts/{int.MaxValue}/positions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Unknown account");
    }
}
