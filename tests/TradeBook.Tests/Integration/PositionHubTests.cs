using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TradeBook.Api.Features.Positions;
using TradeBook.Api.Features.PriceFeed;
using TradeBook.Api.Features.TradeCapture;
using TradeBook.Api.Infrastructure.Auth;
using static TradeBook.Tests.Integration.TradeCaptureTestSupport;

namespace TradeBook.Tests.Integration;

/// <summary>
/// /hubs/positions end to end with the real SignalR client over the in-process
/// test server (design.md sections 8 and 10: subscription authorisation and
/// message delivery). Long polling, because the test server has no sockets.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PositionHubTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(10);

    private TradeBookApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new TradeBookApiFactory(sqlServer.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_subscribed_trader_receives_PositionUpdated_when_a_trade_is_booked()
    {
        var subject = Guid.NewGuid().ToString();
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: subject);
        var token = TestTokens.Create(subject, TradeBookRoles.Trader);
        await using var connection = BuildConnection(token, tokenInQueryString: false);
        var received = new TaskCompletionSource<PositionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<PositionResponse>("PositionUpdated", position => received.TrySetResult(position));

        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeAccount", accountId);

        using var trader = _factory.CreateClientAs(subject, TradeBookRoles.Trader);
        var (booked, _) = await trader.PostTradeAsync(Trade(accountId, "Buy", 300m, 10.00m));
        booked.IsSuccessStatusCode.Should().BeTrue();

        var update = await received.Task.WaitAsync(MessageTimeout);
        update.AccountId.Should().Be(accountId);
        update.InstrumentId.Should().Be(Aapl);
        update.NetQuantity.Should().Be(300m);
        update.AverageCost.Should().Be(10m);
        update.RealisedPnl.Should().Be(0m);
    }

    [Fact]
    public async Task The_token_may_travel_in_the_query_string_as_the_JavaScript_client_sends_it()
    {
        var subject = Guid.NewGuid().ToString();
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: subject);
        await using var connection = BuildConnection(TestTokens.Create(subject, TradeBookRoles.Trader), tokenInQueryString: true);

        await connection.StartAsync();
        var subscribe = () => connection.InvokeAsync("SubscribeAccount", accountId);

        await subscribe.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Subscribing_to_another_traders_account_is_rejected_with_the_access_message()
    {
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: Guid.NewGuid().ToString());
        await using var connection = BuildConnection(TestTokens.Create(Guid.NewGuid().ToString(), TradeBookRoles.Trader), tokenInQueryString: false);
        await connection.StartAsync();

        var subscribe = () => connection.InvokeAsync("SubscribeAccount", accountId);

        (await subscribe.Should().ThrowAsync<HubException>())
            .Which.Message.Should().Contain(AccountAccess.DeniedMessage);
    }

    [Fact]
    public async Task A_rejected_subscriber_receives_nothing_for_that_account()
    {
        var ownerSubject = Guid.NewGuid().ToString();
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: ownerSubject);
        await using var intruder = BuildConnection(TestTokens.Create(Guid.NewGuid().ToString(), TradeBookRoles.Trader), tokenInQueryString: false);
        var leaked = new TaskCompletionSource<PositionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        intruder.On<PositionResponse>("PositionUpdated", position => leaked.TrySetResult(position));
        await intruder.StartAsync();
        try
        {
            await intruder.InvokeAsync("SubscribeAccount", accountId);
        }
        catch (HubException)
        {
            // Expected; the point is what happens next.
        }

        using var owner = _factory.CreateClientAs(ownerSubject, TradeBookRoles.Trader);
        await owner.PostTradeAsync(Trade(accountId, "Buy", 1m, 1m));

        var anything = await Task.WhenAny(leaked.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        anything.Should().NotBeSameAs(leaked.Task, "the intruder never joined the group");
    }

    [Fact]
    public async Task Ops_may_subscribe_to_any_account_and_is_told_when_one_does_not_exist()
    {
        var accountId = await sqlServer.CreateAccountAsync();
        await using var connection = BuildConnection(TestTokens.Create(TestTokens.OpsSubject, TradeBookRoles.Ops), tokenInQueryString: false);
        await connection.StartAsync();

        var subscribeExisting = () => connection.InvokeAsync("SubscribeAccount", accountId);
        var subscribeUnknown = () => connection.InvokeAsync("SubscribeAccount", int.MaxValue);

        await subscribeExisting.Should().NotThrowAsync();
        (await subscribeUnknown.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("No account");
    }

    [Fact]
    public async Task An_anonymous_connection_is_refused()
    {
        await using var connection = BuildConnection(token: null, tokenInQueryString: false);

        var start = () => connection.StartAsync();

        await start.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task A_price_tick_pushes_PositionValued_for_open_positions_only_and_stores_the_new_price()
    {
        var subject = Guid.NewGuid().ToString();
        var accountId = await sqlServer.CreateAccountAsync(ownerSubject: subject);
        var flatInstrumentId = await sqlServer.CreateInstrumentAsync(isActive: true);
        using var trader = _factory.CreateClientAs(subject, TradeBookRoles.Trader);
        await trader.PostTradeAsync(Trade(accountId, "Sell", 200m, 9.00m, instrumentId: 2));           // open short in MSFT
        await trader.PostTradeAsync(Trade(accountId, "Buy", 5m, 1m, instrumentId: flatInstrumentId));  // then flatten it
        await trader.PostTradeAsync(Trade(accountId, "Sell", 5m, 1m, instrumentId: flatInstrumentId));

        await using var connection = BuildConnection(TestTokens.Create(subject, TradeBookRoles.Trader), tokenInQueryString: false);
        var valued = new List<PositionValuedMessage>();
        var firstValuation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<PositionValuedMessage>("PositionValued", message =>
        {
            lock (valued)
            {
                valued.Add(message);
            }

            firstValuation.TrySetResult();
        });
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeAccount", accountId);

        // The feed is disabled in tests; run exactly one tick by hand.
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PriceTick>().RunAsync(CancellationToken.None);
        }

        await firstValuation.Task.WaitAsync(MessageTimeout);
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // let any stragglers arrive

        PositionValuedMessage message;
        lock (valued)
        {
            valued.Should().ContainSingle(m => m.AccountId == accountId, "one open position, one message; the flat one is not marked");
            message = valued.Single(m => m.AccountId == accountId);
        }

        message.InstrumentId.Should().Be(2);
        message.LastPrice.Should().BePositive();
        // (last − 9.00) × −200, rounded to six places, exactly as the REST endpoint computes it.
        message.UnrealisedPnl.Should().Be(Math.Round((message.LastPrice - 9.00m) * -200m, 6, MidpointRounding.ToEven));
        message.AsOfUtc.Kind.Should().Be(DateTimeKind.Utc);

        await using var dbContext = sqlServer.CreateDbContext();
        var stored = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(dbContext.InstrumentPrices, p => p.InstrumentId == 2);
        stored.LastPrice.Should().Be(message.LastPrice, "the pushed price is the stored snapshot");
        stored.AsOfUtc.Should().Be(message.AsOfUtc);
    }

    private HubConnection BuildConnection(string? token, bool tokenInQueryString)
    {
        var url = new Uri(_factory.Server.BaseAddress, "hubs/positions");
        if (token is not null && tokenInQueryString)
        {
            url = new Uri(url + "?access_token=" + Uri.EscapeDataString(token));
        }

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                // Route the client through the in-process server instead of the network.
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null && !tokenInQueryString)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
    }
}
