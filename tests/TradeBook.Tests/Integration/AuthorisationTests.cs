using System.Net;
using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Infrastructure.Auth;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Authentication and the ownership rule across all three account-scoped
/// endpoints (design.md sections 2 and 3). Tokens are minted by
/// <see cref="TestTokens"/>; the API validates them with the real JWT bearer
/// pipeline against a test signing key.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AuthorisationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string AccessDenied = "You do not have access to this account.";

    private TradeBookApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Requests_without_a_token_are_401_problem_details()
    {
        using var anonymous = _factory.CreateClient();

        var (get, getBody) = await anonymous.GetJsonAsync("/api/trades?accountId=1");
        var (post, _) = await anonymous.PostTradeAsync(Trade(1, "Buy", 1m, 1m));
        var (positions, _) = await anonymous.GetJsonAsync("/api/accounts/1/positions");

        get.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        get.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Bearer");
        get.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        getBody.GetProperty("status").GetInt32().Should().Be(401);
        post.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        positions.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_stays_anonymous()
    {
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_signed_in_user_without_a_tradebook_role_is_403()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        using var roleless = _factory.CreateClientAs(Guid.NewGuid().ToString());

        var (response, body) = await roleless.GetJsonAsync($"/api/accounts/{accountId}/positions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.GetProperty("status").GetInt32().Should().Be(403);
    }

    [Fact]
    public async Task A_query_string_token_is_ignored_outside_the_hubs()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        using var anonymous = _factory.CreateClient();
        var token = TestTokens.Create(TestTokens.OpsSubject, TradeBookRoles.Ops);

        var (response, _) = await anonymous.GetJsonAsync($"/api/trades?accountId={accountId}&access_token={token}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_trader_can_book_and_read_their_own_account()
    {
        var subject = Guid.NewGuid().ToString();
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: subject);
        using var trader = _factory.CreateClientAs(subject, TradeBookRoles.Trader);

        var (booked, _) = await trader.PostTradeAsync(Trade(accountId, "Buy", 100m, 10m));
        var (blotter, blotterBody) = await trader.GetJsonAsync($"/api/trades?accountId={accountId}");
        var (positions, positionsBody) = await trader.GetJsonAsync($"/api/accounts/{accountId}/positions");

        booked.StatusCode.Should().Be(HttpStatusCode.Created);
        blotter.StatusCode.Should().Be(HttpStatusCode.OK);
        blotterBody.GetProperty("totalCount").GetInt32().Should().Be(1);
        positions.StatusCode.Should().Be(HttpStatusCode.OK);
        positionsBody.GetArrayLength().Should().Be(1);

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.SingleAsync(t => t.AccountId == accountId)).CapturedBySubject.Should().Be(subject);
    }

    [Fact]
    public async Task A_trader_is_refused_another_traders_account_everywhere_and_nothing_is_written()
    {
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: Guid.NewGuid().ToString());
        using var intruder = _factory.CreateClientAs(Guid.NewGuid().ToString(), TradeBookRoles.Trader);

        var (booked, bookedBody) = await intruder.PostTradeAsync(Trade(accountId, "Buy", 100m, 10m));
        var (blotter, blotterBody) = await intruder.GetJsonAsync($"/api/trades?accountId={accountId}");
        var (positions, positionsBody) = await intruder.GetJsonAsync($"/api/accounts/{accountId}/positions");

        foreach (var (response, body) in new[] { (booked, bookedBody), (blotter, blotterBody), (positions, positionsBody) })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            body.GetProperty("detail").GetString().Should().Be(AccessDenied);
        }

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.AnyAsync(t => t.AccountId == accountId)).Should().BeFalse();
    }

    [Fact]
    public async Task A_trader_gets_403_not_404_for_an_account_that_does_not_exist()
    {
        using var trader = _factory.CreateClientAs(Guid.NewGuid().ToString(), TradeBookRoles.Trader);

        var (booked, _) = await trader.PostTradeAsync(Trade(int.MaxValue, "Buy", 100m, 10m));
        var (blotter, _) = await trader.GetJsonAsync($"/api/trades?accountId={int.MaxValue}");
        var (positions, _) = await trader.GetJsonAsync($"/api/accounts/{int.MaxValue}/positions");

        booked.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the response must not reveal whether the account exists");
        blotter.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        positions.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ops_may_use_any_account_and_is_told_when_one_does_not_exist()
    {
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: Guid.NewGuid().ToString());
        using var ops = _factory.CreateOpsClient();

        var (booked, _) = await ops.PostTradeAsync(Trade(accountId, "Buy", 100m, 10m));
        var (blotter, _) = await ops.GetJsonAsync($"/api/trades?accountId={accountId}");
        var (positions, _) = await ops.GetJsonAsync($"/api/accounts/{accountId}/positions");
        var (unknown, unknownBody) = await ops.GetJsonAsync($"/api/accounts/{int.MaxValue}/positions");

        booked.StatusCode.Should().Be(HttpStatusCode.Created);
        blotter.StatusCode.Should().Be(HttpStatusCode.OK);
        positions.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownBody.GetProperty("title").GetString().Should().Be("Unknown account");
    }

    [Fact]
    public async Task A_trader_cannot_replay_another_traders_external_reference()
    {
        var ownerSubject = Guid.NewGuid().ToString();
        var ownersAccount = await sqlServer.CreateAccountAsync(ownerSubject: ownerSubject);
        var intruderSubject = Guid.NewGuid().ToString();
        var intrudersAccount = await sqlServer.CreateAccountAsync(ownerSubject: intruderSubject);
        var externalRef = $"OMS-{Guid.NewGuid():N}"[..20];
        using var owner = _factory.CreateClientAs(ownerSubject, TradeBookRoles.Trader);
        using var intruder = _factory.CreateClientAs(intruderSubject, TradeBookRoles.Trader);

        var (original, _) = await owner.PostTradeAsync(Trade(ownersAccount, "Buy", 100m, 10m, externalRef));
        // Same reference, but on the intruder's own account: the original lives
        // on an account they may not see, so they learn nothing about it.
        var (replay, replayBody) = await intruder.PostTradeAsync(Trade(intrudersAccount, "Buy", 100m, 10m, externalRef));

        original.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        replayBody.GetProperty("detail").GetString().Should().Be(AccessDenied);

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.CountAsync(t => t.ExternalRef == externalRef)).Should().Be(1);
    }
}
