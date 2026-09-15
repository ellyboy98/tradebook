using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TradeBook.Api.Infrastructure.Auth;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>GET/POST /api/instruments and /api/accounts (design.md section 7; GET /api/accounts per ADR-015).</summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ReferenceDataTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TradeBookApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_trader_lists_active_instruments_with_their_latest_quote()
    {
        var deadInstrumentId = await sqlServer.CreateInstrumentAsync(isActive: false);
        using var trader = _factory.CreateClientAs(Guid.NewGuid().ToString(), TradeBookRoles.Trader);

        var (response, body) = await trader.GetJsonAsync("/api/instruments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = body.EnumerateArray().ToList();
        var aapl = rows.Should().ContainSingle(r => r.GetProperty("symbol").GetString() == "AAPL").Subject;
        aapl.GetProperty("lastPrice").GetDecimal().Should().Be(11.02m, "seeded quote; the feed is off in tests");
        aapl.GetProperty("instrumentType").GetString().Should().Be("Equity");
        rows.Should().NotContain(r => r.GetProperty("id").GetInt32() == deadInstrumentId, "inactive instruments are not tradeable");
        rows.Select(r => r.GetProperty("symbol").GetString()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Only_ops_may_create_instruments_and_the_new_one_is_quoted_from_its_initial_price()
    {
        var symbol = $"N{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var request = new { symbol, name = "New Co", currency = "USD", tickSize = 0.01m, lotSize = 1, initialPrice = 25.50m };
        using var trader = _factory.CreateClientAs(Guid.NewGuid().ToString(), TradeBookRoles.Trader);
        using var ops = _factory.CreateOpsClient();

        using var refused = await trader.PostAsJsonAsync("/api/instruments", request);
        using var created = await ops.PostAsJsonAsync("/api/instruments", request);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var (_, listed) = await trader.GetJsonAsync("/api/instruments");

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        createdBody.GetProperty("symbol").GetString().Should().Be(symbol);
        createdBody.GetProperty("lastPrice").GetDecimal().Should().Be(25.50m);
        var quoted = listed.EnumerateArray().Should().ContainSingle(r => r.GetProperty("symbol").GetString() == symbol).Subject;
        quoted.GetProperty("lastPrice").GetDecimal().Should().Be(25.50m);
    }

    [Fact]
    public async Task Creating_an_instrument_with_an_existing_symbol_is_409()
    {
        using var ops = _factory.CreateOpsClient();

        using var response = await ops.PostAsJsonAsync("/api/instruments",
            new { symbol = "aapl", name = "Apple again", currency = "USD", tickSize = 0.01m, lotSize = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "symbols are compared upper-cased");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Rejects_a_malformed_instrument_naming_the_fields()
    {
        using var ops = _factory.CreateOpsClient();

        using var response = await ops.PostAsJsonAsync("/api/instruments",
            new { symbol = "X", name = "X", currency = "usd", tickSize = 0m, lotSize = 0 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errors").EnumerateObject().Select(e => e.Name)
            .Should().BeEquivalentTo("Currency", "TickSize", "LotSize");
    }

    [Fact]
    public async Task A_trader_lists_only_their_own_accounts_and_ops_lists_all()
    {
        var subject = Guid.NewGuid().ToString();
        var mine = await sqlServer.CreateAccountAsync(ownerSubject: subject);
        var theirs = await sqlServer.CreateAccountAsync(ownerSubject: Guid.NewGuid().ToString());
        using var trader = _factory.CreateClientAs(subject, TradeBookRoles.Trader);
        using var ops = _factory.CreateOpsClient();

        var (_, traderBody) = await trader.GetJsonAsync("/api/accounts");
        var (_, opsBody) = await ops.GetJsonAsync("/api/accounts");

        var traderIds = traderBody.EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();
        traderIds.Should().Equal(mine);
        var opsIds = opsBody.EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();
        opsIds.Should().Contain([mine, theirs]);
        opsBody.EnumerateArray().Select(a => a.GetProperty("code").GetString()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Only_ops_may_create_accounts_and_the_named_owner_can_then_use_it()
    {
        var ownerSubject = Guid.NewGuid().ToString();
        var code = $"N-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var request = new { code, name = "New desk", baseCurrency = "USD", ownerSubject };
        using var trader = _factory.CreateClientAs(ownerSubject, TradeBookRoles.Trader);
        using var ops = _factory.CreateOpsClient();

        using var refused = await trader.PostAsJsonAsync("/api/accounts", request);
        using var created = await ops.PostAsJsonAsync("/api/accounts", request);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var newId = createdBody.GetProperty("id").GetInt32();
        var (positions, _) = await trader.GetJsonAsync($"/api/accounts/{newId}/positions");

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        createdBody.GetProperty("code").GetString().Should().Be(code);
        createdBody.GetProperty("ownerSubject").GetString().Should().Be(ownerSubject);
        positions.StatusCode.Should().Be(HttpStatusCode.OK, "the account belongs to the named subject straight away");
    }

    [Fact]
    public async Task Creating_an_account_with_an_existing_code_is_409()
    {
        using var ops = _factory.CreateOpsClient();

        using var response = await ops.PostAsJsonAsync("/api/accounts",
            new { code = "eq-desk-1", name = "Again", baseCurrency = "USD", ownerSubject = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
