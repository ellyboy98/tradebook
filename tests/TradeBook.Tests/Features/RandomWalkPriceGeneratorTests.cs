using TradeBook.Api.Features.PriceFeed;

namespace TradeBook.Tests.Features;

[Trait("Category", "Unit")]
public sealed class RandomWalkPriceGeneratorTests
{
    [Fact]
    public void Moves_by_at_most_the_volatility_and_lands_on_the_tick_grid()
    {
        var generator = new RandomWalkPriceGenerator(volatility: 0.002m, new Random(42));

        for (var i = 0; i < 1_000; i++)
        {
            var next = generator.NextPrice(11.02m, 0.01m);

            next.Should().BeInRange(11.02m * 0.998m - 0.01m, 11.02m * 1.002m + 0.01m);
            (next % 0.01m).Should().Be(0m, "prices sit on the tick grid");
        }
    }

    [Fact]
    public void Zero_volatility_returns_the_same_price()
    {
        var generator = new RandomWalkPriceGenerator(volatility: 0m, new Random(1));

        generator.NextPrice(42.10m, 0.01m).Should().Be(42.10m);
    }

    [Fact]
    public void Never_falls_below_one_tick()
    {
        var generator = new RandomWalkPriceGenerator(volatility: 0.9m, new Random(7));
        var price = 0.05m;

        for (var i = 0; i < 10_000; i++)
        {
            price = generator.NextPrice(price, 0.01m);
            price.Should().BeGreaterThanOrEqualTo(0.01m);
        }
    }

    [Fact]
    public void Is_reproducible_for_a_given_seed()
    {
        var first = new RandomWalkPriceGenerator(0.01m, new Random(123));
        var second = new RandomWalkPriceGenerator(0.01m, new Random(123));

        var sequenceA = Enumerable.Range(0, 20).Select(_ => first.NextPrice(100m, 0.01m)).ToList();
        var sequenceB = Enumerable.Range(0, 20).Select(_ => second.NextPrice(100m, 0.01m)).ToList();

        sequenceA.Should().Equal(sequenceB);
    }

    [Theory]
    [InlineData(0, 0.01)]
    [InlineData(10, 0)]
    public void Rejects_non_positive_inputs(double lastPrice, double tickSize)
    {
        var generator = new RandomWalkPriceGenerator(0.01m, new Random(1));

        var act = () => generator.NextPrice((decimal)lastPrice, (decimal)tickSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
