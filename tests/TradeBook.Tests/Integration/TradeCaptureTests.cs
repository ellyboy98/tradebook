using System.Net;
using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>
/// POST /api/trades end to end against a real SQL Server. Each test creates
/// its own account so positions never bleed between tests. Assertions read the
/// raw JSON so they pin the wire contract (design.md section 7), not the C#
/// types.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TradeCaptureTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TradeBookApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Buying_from_flat_returns_201_with_the_trade_and_a_new_position()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var trade = body.GetProperty("trade");
        trade.GetProperty("id").GetInt64().Should().BePositive();
        trade.GetProperty("accountId").GetInt32().Should().Be(accountId);
        trade.GetProperty("instrumentId").GetInt32().Should().Be(Aapl);
        trade.GetProperty("side").GetString().Should().Be("Buy");
        trade.GetProperty("quantity").GetDecimal().Should().Be(300m);
        trade.GetProperty("price").GetDecimal().Should().Be(10.00m);
        trade.GetProperty("executedAtUtc").GetString().Should().Be("2026-09-01T02:31:00Z");

        var position = body.GetProperty("position");
        position.GetProperty("accountId").GetInt32().Should().Be(accountId);
        position.GetProperty("instrumentId").GetInt32().Should().Be(Aapl);
        position.GetProperty("netQuantity").GetDecimal().Should().Be(300m);
        position.GetProperty("averageCost").GetDecimal().Should().Be(10.00m);
        position.GetProperty("realisedPnl").GetDecimal().Should().Be(0m);

        await using var dbContext = sqlServer.CreateDbContext();
        var storedTrade = await dbContext.Trades.SingleAsync(t => t.AccountId == accountId);
        var storedPosition = await dbContext.Positions.SingleAsync(p => p.AccountId == accountId);

        storedTrade.Side.Should().Be(TradeSide.Buy);
        storedTrade.ExecutedAtUtc.Should().Be(ExecutedAt.UtcDateTime);
        storedTrade.CapturedBySubject.Should().NotBeNullOrWhiteSpace();
        storedTrade.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        storedTrade.CapturedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        storedPosition.NetQuantity.Should().Be(300m);
        storedPosition.AverageCost.Should().Be(10m);
        storedPosition.RealisedPnl.Should().Be(0m);
        storedPosition.LastTradeId.Should().Be(storedTrade.Id);
        storedPosition.RowVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Worked_example_through_the_api_matches_design_section_5()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        (string Side, decimal Quantity, decimal Price, decimal Net, decimal Average, decimal Realised)[] script =
        [
            ("Buy", 300m, 10.00m, 300m, 10.00m, 0m),
            ("Buy", 200m, 12.00m, 500m, 10.80m, 0m),
            ("Sell", 100m, 13.00m, 400m, 10.80m, 220m),
            ("Sell", 600m, 9.00m, -200m, 9.00m, -500m),
            ("Buy", 50m, 8.00m, -150m, 9.00m, -450m),
            ("Buy", 150m, 9.50m, 0m, 0m, -525m),
        ];

        for (var i = 0; i < script.Length; i++)
        {
            var (side, quantity, price, net, average, realised) = script[i];

            var (response, body) = await _client.PostTradeAsync(Trade(accountId, side, quantity, price));

            response.StatusCode.Should().Be(HttpStatusCode.Created, "trade {0} should book", i + 1);
            var position = body.GetProperty("position");
            position.GetProperty("netQuantity").GetDecimal().Should().Be(net, "after trade {0}", i + 1);
            position.GetProperty("averageCost").GetDecimal().Should().Be(average, "after trade {0}", i + 1);
            position.GetProperty("realisedPnl").GetDecimal().Should().Be(realised, "after trade {0}", i + 1);
        }

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.CountAsync(t => t.AccountId == accountId)).Should().Be(6);
        var stored = await dbContext.Positions.SingleAsync(p => p.AccountId == accountId);
        stored.NetQuantity.Should().Be(0m);
        stored.AverageCost.Should().Be(0m);
        stored.RealisedPnl.Should().Be(-525m);
    }

    [Fact]
    public async Task Repeating_an_external_ref_returns_the_original_trade_without_booking_again()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        var externalRef = $"OMS-{Guid.NewGuid():N}"[..20];

        var (first, firstBody) = await _client.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m, externalRef));
        // Same reference, different body: the design says the original wins.
        var (second, secondBody) = await _client.PostTradeAsync(Trade(accountId, "Buy", 999m, 99.00m, externalRef));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstTrade = firstBody.GetProperty("trade");
        var secondTrade = secondBody.GetProperty("trade");
        secondTrade.GetProperty("id").GetInt64().Should().Be(firstTrade.GetProperty("id").GetInt64());
        secondTrade.GetProperty("quantity").GetDecimal().Should().Be(300m);
        secondTrade.GetProperty("externalRef").GetString().Should().Be(externalRef);
        secondBody.GetProperty("position").GetProperty("netQuantity").GetDecimal().Should().Be(300m);

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.CountAsync(t => t.AccountId == accountId)).Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Rejects_a_quantity_that_is_not_positive(int quantity)
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Buy", quantity, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldErrors(body, "quantity").Should().ContainSingle().Which.Should().Be("Quantity must be greater than zero.");
    }

    [Fact]
    public async Task Rejects_a_price_that_is_not_positive()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Buy", 100m, 0m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldErrors(body, "price").Should().ContainSingle().Which.Should().Be("Price must be greater than zero.");
    }

    [Fact]
    public async Task Rejects_an_execution_time_in_the_future()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(
            Trade(accountId, "Buy", 100m, 10m, executedAt: DateTimeOffset.UtcNow.AddMinutes(5)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldErrors(body, "executedAtUtc").Should().ContainSingle();
    }

    [Fact]
    public async Task Rejects_an_inactive_instrument()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        var deadInstrumentId = await sqlServer.CreateInstrumentAsync(isActive: false);

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Buy", 100m, 10m, instrumentId: deadInstrumentId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldErrors(body, "instrumentId").Should().ContainSingle().Which.Should().Be("This instrument is no longer tradeable.");
    }

    [Fact]
    public async Task Reports_every_failing_field_at_once()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Sell", 0m, 0m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errors").EnumerateObject().Select(e => e.Name).Should().BeEquivalentTo("quantity", "price");
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_account()
    {
        var (response, body) = await _client.PostTradeAsync(Trade(int.MaxValue, "Buy", 100m, 10m));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.GetProperty("title").GetString().Should().Be("Unknown account");
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_instrument()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.PostTradeAsync(Trade(accountId, "Buy", 100m, 10m, instrumentId: int.MaxValue));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Unknown instrument");
    }

    [Fact]
    public async Task Rejects_a_side_that_is_not_buy_or_sell()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, _) = await _client.PostTradeAsync(Trade(accountId, "Hold", 100m, 10m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rejects_a_body_with_a_missing_field()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, _) = await _client.PostTradeAsync(new { accountId, instrumentId = Aapl, side = "Buy", quantity = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
