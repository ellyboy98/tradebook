using System.Net;
using System.Text.Json;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>GET /api/trades: filtering, ordering, paging and validation (design.md section 7, ADR-012).</summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BlotterTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const int Msft = 2; // seeded instrument

    private static readonly DateTimeOffset Sep1At09 = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sep1At10 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sep2At09 = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sep3At09 = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

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
    public async Task Returns_the_accounts_trades_newest_first_with_paging_metadata()
    {
        var accountId = await BookFourTradesAsync();
        // Another account's trade must not leak in.
        var otherAccountId = await sqlServer.CreateAccountAsync();
        await _client.PostTradeAsync(Trade(otherAccountId, "Buy", 1m, 1m, externalRef: $"OTHER-{otherAccountId}"));

        var (response, body) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
        body.GetProperty("totalCount").GetInt32().Should().Be(4);
        Refs(body).Should().Equal("T4", "T3", "T2", "T1");

        var newest = body.GetProperty("items")[0];
        newest.GetProperty("accountId").GetInt32().Should().Be(accountId);
        newest.GetProperty("side").GetString().Should().Be("Buy");
        newest.GetProperty("executedAtUtc").GetString().Should().Be("2026-09-03T09:00:00Z");
    }

    [Fact]
    public async Task Filters_by_instrument()
    {
        var accountId = await BookFourTradesAsync();

        var (_, body) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}&instrumentId={Aapl}");

        Refs(body).Should().Equal("T4", "T2", "T1");
        body.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Date_range_is_inclusive_of_from_and_exclusive_of_to()
    {
        var accountId = await BookFourTradesAsync();

        // T2 is exactly at fromUtc (kept); T4 is exactly at toUtc (dropped).
        var (_, body) = await _client.GetJsonAsync(
            $"/api/trades?accountId={accountId}&fromUtc={Uri.EscapeDataString(Sep1At10.ToString("O"))}&toUtc={Uri.EscapeDataString(Sep3At09.ToString("O"))}");

        Refs(body).Should().Equal("T3", "T2");
    }

    [Fact]
    public async Task Pages_without_losing_or_repeating_trades()
    {
        var accountId = await BookFourTradesAsync();

        var (_, page1) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}&pageSize=3&page=1");
        var (_, page2) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}&pageSize=3&page=2");
        var (_, page3) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}&pageSize=3&page=3");

        Refs(page1).Should().Equal("T4", "T3", "T2");
        Refs(page2).Should().Equal("T1");
        Refs(page3).Should().BeEmpty();
        page2.GetProperty("totalCount").GetInt32().Should().Be(4);
        page2.GetProperty("page").GetInt32().Should().Be(2);
        page2.GetProperty("pageSize").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("", "AccountId")]
    [InlineData("accountId=1&pageSize=201", "PageSize")]
    [InlineData("accountId=1&pageSize=0", "PageSize")]
    [InlineData("accountId=1&page=0", "Page")]
    [InlineData("accountId=1&fromUtc=2026-09-03T00:00:00Z&toUtc=2026-09-01T00:00:00Z", "FromUtc")]
    public async Task Rejects_a_malformed_query_naming_the_field(string queryString, string field)
    {
        var (response, body) = await _client.GetJsonAsync($"/api/trades?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue("the error should name {0}", field);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_account()
    {
        var (response, body) = await _client.GetJsonAsync($"/api/trades?accountId={int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Unknown account");
    }

    [Fact]
    public async Task Returns_an_empty_page_for_an_account_with_no_trades()
    {
        var accountId = await sqlServer.CreateAccountAsync();

        var (response, body) = await _client.GetJsonAsync($"/api/trades?accountId={accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// T1 Buy AAPL Sep 1 09:00, T2 Sell AAPL Sep 1 10:00, T3 Buy MSFT Sep 2
    /// 09:00, T4 Buy AAPL Sep 3 09:00. Booked oldest first so ids ascend with
    /// time; the external reference is unique per account so each test has
    /// its own.
    /// </summary>
    private async Task<int> BookFourTradesAsync()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        var prefix = $"{accountId}-";

        (string Ref, string Side, decimal Qty, decimal Px, DateTimeOffset At, int Instrument)[] script =
        [
            ("T1", "Buy", 100m, 10m, Sep1At09, Aapl),
            ("T2", "Sell", 50m, 11m, Sep1At10, Aapl),
            ("T3", "Buy", 200m, 8m, Sep2At09, Msft),
            ("T4", "Buy", 10m, 12m, Sep3At09, Aapl),
        ];

        foreach (var (reference, side, quantity, price, at, instrument) in script)
        {
            var (response, _) = await _client.PostTradeAsync(
                Trade(accountId, side, quantity, price, externalRef: prefix + reference, executedAt: at, instrumentId: instrument));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        return accountId;
    }

    /// <summary>The trade labels (T1..T4) in the order the page returned them.</summary>
    private static IEnumerable<string> Refs(JsonElement page) => page
        .GetProperty("items")
        .EnumerateArray()
        .Select(item => item.GetProperty("externalRef").GetString()!)
        .Select(reference => reference[(reference.IndexOf('-') + 1)..]);
}
