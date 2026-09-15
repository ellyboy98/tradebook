using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Design.md sections 6 and 10: two executions arrive at once for the same
/// account and instrument. Both must be recorded and the final net quantity
/// must be their sum.
/// </summary>
/// <remarks>
/// Left to chance, two HTTP requests may or may not overlap in the few
/// milliseconds between reading the position and saving it, so the tests
/// would pass or fail at random. <see cref="PositionReadInterceptor"/> removes
/// the chance: it holds each request the moment it has read the position
/// until the other has read it too, so both start from the same row_version
/// and the second UPDATE is guaranteed to affect zero rows. Until
/// <c>CaptureTradeHandler</c> catches <c>DbUpdateConcurrencyException</c>,
/// reloads and retries (build step 5), the second request returns 500 and
/// these tests fail. That is their purpose.
/// </remarks>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TradeCaptureConcurrencyTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Two_simultaneous_executions_on_one_position_are_both_recorded()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        await OpenPositionAsync(accountId);

        var barrier = new PositionReadBarrier(parties: 2);
        var interceptor = new PositionReadInterceptor(barrier.ArriveAsync);
        await using var factory = new TradeBookApiFactory(
            sqlServer.ConnectionString,
            services => services.AddSingleton<IInterceptor>(interceptor));
        using var client = factory.CreateOpsClient();

        // Buying at the current average cost and selling above it gives the
        // same final state whichever request commits first, so the assertion
        // does not depend on who wins the race.
        var buy = client.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m));
        var sell = client.PostTradeAsync(Trade(accountId, "Sell", 50m, 12.00m));
        var results = await Task.WhenAll(buy, sell);

        results.Select(r => r.Response.StatusCode).Should().OnlyContain(status => status == HttpStatusCode.Created);

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.CountAsync(t => t.AccountId == accountId)).Should().Be(3, "the opening trade plus both simultaneous ones");
        var position = await dbContext.Positions.SingleAsync(p => p.AccountId == accountId);
        position.NetQuantity.Should().Be(350m, "100 + 300 − 50");
        position.AverageCost.Should().Be(10m);
        position.RealisedPnl.Should().Be(100m, "50 × (12.00 − 10.00)");
    }

    [Fact]
    public async Task Gives_up_after_three_attempts_and_returns_409_leaving_nothing_behind()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        await OpenPositionAsync(accountId);

        // Every time the request reads the position, change the row from a
        // second connection so its row_version moves on. The request can never
        // win; the design says it stops after three attempts and returns 409.
        var interceptor = new PositionReadInterceptor(() => TouchPositionAsync(accountId));
        await using var factory = new TradeBookApiFactory(
            sqlServer.ConnectionString,
            services => services.AddSingleton<IInterceptor>(interceptor));
        using var client = factory.CreateOpsClient();

        var (response, body) = await client.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.GetProperty("detail").GetString().Should().Be("The position changed while this was submitted. Book it again.");
        interceptor.PositionReads.Should().Be(3, "three attempts, each reading the position once");

        await using var dbContext = sqlServer.CreateDbContext();
        (await dbContext.Trades.CountAsync(t => t.AccountId == accountId)).Should().Be(1, "a failed attempt must leave nothing behind");
        (await dbContext.Positions.SingleAsync(p => p.AccountId == accountId)).NetQuantity.Should().Be(100m);
    }

    /// <summary>
    /// Books one trade through a plain API instance so the position row exists.
    /// The race under test is two UPDATEs of one row, not two INSERTs; two
    /// simultaneous first trades are a separate case (see the step 5 notes).
    /// </summary>
    private async Task OpenPositionAsync(int accountId)
    {
        await using var factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        using var client = factory.CreateOpsClient();

        var (response, _) = await client.PostTradeAsync(Trade(accountId, "Buy", 100m, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Any UPDATE moves a rowversion on, even one that changes nothing the
    /// application cares about.
    /// </summary>
    private async Task TouchPositionAsync(int accountId)
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE positions SET updated_at_utc = SYSUTCDATETIME() WHERE account_id = @accountId";
        command.Parameters.AddWithValue("@accountId", accountId);
        await command.ExecuteNonQueryAsync();
    }
}
