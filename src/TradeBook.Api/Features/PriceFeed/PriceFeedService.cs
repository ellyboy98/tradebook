using Microsoft.Extensions.Options;

namespace TradeBook.Api.Features.PriceFeed;

/// <summary>
/// The hosted loop that runs a <see cref="PriceTick"/> every interval.
/// </summary>
/// <remarks>
/// A hosted service is a singleton that lives as long as the process, while
/// DbContext is scoped to one unit of work. Injecting the context here would
/// make one context live forever, accumulating tracked entities and holding
/// a stale view of the data; ASP.NET Core refuses it at startup for exactly
/// that reason. So every tick creates its own scope and resolves a fresh
/// <see cref="PriceTick"/> (and with it a fresh DbContext) from it.
/// </remarks>
public sealed class PriceFeedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PriceFeedOptions> options,
    ILogger<PriceFeedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var feed = options.Value;
        if (!feed.Enabled)
        {
            logger.LogInformation("Price feed is disabled by configuration");
            return;
        }

        logger.LogInformation(
            "Price feed ticking every {IntervalSeconds}s with volatility {Volatility}",
            feed.IntervalSeconds,
            feed.Volatility);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(feed.IntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var tick = scope.ServiceProvider.GetRequiredService<PriceTick>();
                    await tick.RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad tick (SQL Server restarting, say) must not end the
                    // feed for the life of the process. Log it and try again.
                    logger.LogError(ex, "Price tick failed; will retry on the next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: the host cancelled stoppingToken.
        }

        logger.LogInformation("Price feed stopped");
    }
}
