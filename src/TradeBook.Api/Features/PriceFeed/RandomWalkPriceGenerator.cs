using Microsoft.Extensions.Options;

namespace TradeBook.Api.Features.PriceFeed;

/// <summary>
/// A symmetric random walk: each tick moves the price by a uniformly random
/// fraction of itself in [−volatility, +volatility], snapped to the tick size
/// and never below one tick, so a price can wander but cannot reach zero.
/// </summary>
public sealed class RandomWalkPriceGenerator : IPriceGenerator
{
    private readonly decimal _volatility;
    private readonly Random _random;

    /// <summary>The constructor DI uses.</summary>
    public RandomWalkPriceGenerator(IOptions<PriceFeedOptions> options)
        : this(options.Value.Volatility, Random.Shared)
    {
    }

    /// <summary>For tests: a seeded <see cref="Random"/> makes the walk reproducible.</summary>
    public RandomWalkPriceGenerator(decimal volatility, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volatility);
        _volatility = volatility;
        _random = random;
    }

    public decimal NextPrice(decimal lastPrice, decimal tickSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lastPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickSize);

        // NextDouble is [0, 1); stretch it to [-1, 1).
        var direction = (decimal)(_random.NextDouble() * 2 - 1);
        var moved = lastPrice * (1 + _volatility * direction);

        // Real prices sit on a grid of tick sizes. Round to the nearest tick,
        // then refuse to go below the first one.
        var ticks = Math.Max(1m, Math.Round(moved / tickSize, 0, MidpointRounding.ToEven));
        return ticks * tickSize;
    }
}
