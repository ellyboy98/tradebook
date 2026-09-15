using TradeBook.Api.Domain;

namespace TradeBook.Tests.Domain;

/// <summary>
/// Drives <c>Domain/PositionMath.cs</c> and <c>Domain/PositionState.cs</c>,
/// which the owner writes by hand (CLAUDE.md, build step 3). Expected values
/// are design.md section 5 transcribed; the extra cases cover the rules the
/// worked example only exercises in one direction.
/// </summary>
/// <remarks>
/// The shape these tests expect, all plain C# with no package references:
/// <code>
/// public sealed record PositionState(decimal NetQuantity, decimal AverageCost, decimal RealisedPnl)
/// {
///     public static PositionState Flat { get; } = new(0m, 0m, 0m);
/// }
///
/// public static class PositionMath
/// {
///     // Applies one execution and returns the new state. Quantity is the
///     // stored, always-positive value; the sign comes from side. Average
///     // cost and realised P&amp;L are rounded to six places, half to even.
///     public static PositionState Apply(PositionState state, TradeSide side, decimal quantity, decimal price);
///
///     // (lastPrice − AverageCost) × NetQuantity
///     public static decimal UnrealisedPnl(PositionState state, decimal lastPrice);
/// }
/// </code>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class PositionMathTests
{
    // Design section 5, "Worked example — run this as the test suite".
    private static readonly (TradeSide Side, decimal Quantity, decimal Price)[] WorkedExample =
    [
        (TradeSide.Buy, 300m, 10.00m),
        (TradeSide.Buy, 200m, 12.00m),
        (TradeSide.Sell, 100m, 13.00m),
        (TradeSide.Sell, 600m, 9.00m),
        (TradeSide.Buy, 50m, 8.00m),
        (TradeSide.Buy, 150m, 9.50m),
    ];

    // After trade #, net quantity, average cost, realised. One row per line
    // of the table in design.md so a failure names the line.
    public static TheoryData<int, decimal, decimal, decimal> WorkedExampleExpectations => new()
    {
        { 1, 300m, 10.00m, 0.00m },
        { 2, 500m, 10.80m, 0.00m },
        { 3, 400m, 10.80m, 220.00m },
        { 4, -200m, 9.00m, -500.00m },
        { 5, -150m, 9.00m, -450.00m },
        { 6, 0m, 0.00m, -525.00m },
    };

    [Theory]
    [MemberData(nameof(WorkedExampleExpectations))]
    public void Worked_example_matches_design_section_5(
        int afterTrade, decimal netQuantity, decimal averageCost, decimal realisedPnl)
    {
        var state = PositionState.Flat;
        foreach (var (side, quantity, price) in WorkedExample.Take(afterTrade))
        {
            state = PositionMath.Apply(state, side, quantity, price);
        }

        state.Should().Be(new PositionState(netQuantity, averageCost, realisedPnl));
    }

    [Fact]
    public void Flat_state_is_all_zeros()
    {
        PositionState.Flat.Should().Be(new PositionState(0m, 0m, 0m));
    }

    // Opening from flat (N = 0): N' = q, A' = p, R' = R.

    [Fact]
    public void Opening_long_from_flat_takes_trade_price_as_average_cost()
    {
        var result = PositionMath.Apply(PositionState.Flat, TradeSide.Buy, 100m, 12.50m);

        result.Should().Be(new PositionState(100m, 12.50m, 0m));
    }

    [Fact]
    public void Opening_short_from_flat_gives_negative_net_quantity()
    {
        var result = PositionMath.Apply(PositionState.Flat, TradeSide.Sell, 100m, 12.50m);

        result.Should().Be(new PositionState(-100m, 12.50m, 0m));
    }

    [Fact]
    public void Opening_from_flat_preserves_previously_realised_pnl()
    {
        // A position that was closed and reopened keeps its lifetime realised total.
        var flatWithHistory = new PositionState(0m, 0m, -525m);

        var result = PositionMath.Apply(flatWithHistory, TradeSide.Buy, 10m, 9m);

        result.Should().Be(new PositionState(10m, 9m, -525m));
    }

    // Increasing (same sign): N' = N + q, A' = (|N|·A + |q|·p) / |N'|, R' = R.

    [Fact]
    public void Increasing_a_long_blends_average_cost_by_quantity()
    {
        var result = PositionMath.Apply(new PositionState(300m, 10.00m, 0m), TradeSide.Buy, 200m, 12.00m);

        // (300 × 10.00 + 200 × 12.00) ÷ 500 = 10.80
        result.Should().Be(new PositionState(500m, 10.80m, 0m));
    }

    [Fact]
    public void Increasing_a_short_blends_average_cost_and_realises_nothing()
    {
        var result = PositionMath.Apply(new PositionState(-100m, 10.00m, 0m), TradeSide.Sell, 100m, 12.00m);

        // (100 × 10.00 + 100 × 12.00) ÷ 200 = 11.00
        result.Should().Be(new PositionState(-200m, 11.00m, 0m));
    }

    // Reducing without crossing zero (opposite sign, |q| ≤ |N|):
    // R' = R + |q| × (p − A) × sign(N), N' = N + q, A' = A (or 0 when N' = 0).

    [Fact]
    public void Reducing_a_long_realises_against_average_cost_and_keeps_it()
    {
        var result = PositionMath.Apply(new PositionState(500m, 10.80m, 0m), TradeSide.Sell, 100m, 13.00m);

        // 100 × (13.00 − 10.80) × (+1) = 220.00
        result.Should().Be(new PositionState(400m, 10.80m, 220.00m));
    }

    [Fact]
    public void Reducing_a_short_at_a_lower_price_realises_a_gain()
    {
        var result = PositionMath.Apply(new PositionState(-200m, 9.00m, -500m), TradeSide.Buy, 50m, 8.00m);

        // 50 × (8.00 − 9.00) × (−1) = +50.00
        result.Should().Be(new PositionState(-150m, 9.00m, -450m));
    }

    [Fact]
    public void Reducing_a_short_at_a_higher_price_realises_a_loss()
    {
        var result = PositionMath.Apply(new PositionState(-100m, 10.00m, 0m), TradeSide.Buy, 40m, 12.00m);

        // 40 × (12.00 − 10.00) × (−1) = −80.00
        result.Should().Be(new PositionState(-60m, 10.00m, -80m));
    }

    [Fact]
    public void Closing_a_long_exactly_to_zero_resets_average_cost()
    {
        var result = PositionMath.Apply(new PositionState(400m, 10.80m, 220m), TradeSide.Sell, 400m, 11.00m);

        // 400 × (11.00 − 10.80) × (+1) = 80.00; flat, so average cost is 0
        result.Should().Be(new PositionState(0m, 0m, 300m));
    }

    [Fact]
    public void Closing_a_short_exactly_to_zero_resets_average_cost()
    {
        var result = PositionMath.Apply(new PositionState(-150m, 9.00m, -450m), TradeSide.Buy, 150m, 9.50m);

        // 150 × (9.50 − 9.00) × (−1) = −75.00; flat, so average cost is 0
        result.Should().Be(new PositionState(0m, 0m, -525m));
    }

    // Crossing zero (opposite sign, |q| > |N|):
    // R' = R + |N| × (p − A) × sign(N), N' = N + q, A' = p.
    // The old average cost is discarded, not blended with the new side.

    [Fact]
    public void Crossing_from_long_to_short_realises_the_closed_quantity_and_opens_at_trade_price()
    {
        var result = PositionMath.Apply(new PositionState(400m, 10.80m, 220m), TradeSide.Sell, 600m, 9.00m);

        // closes 400 × (9.00 − 10.80) = −720.00 → realised −500.00; remaining 200 short at 9.00
        result.Should().Be(new PositionState(-200m, 9.00m, -500m));
    }

    [Fact]
    public void Crossing_from_short_to_long_realises_the_closed_quantity_and_opens_at_trade_price()
    {
        var result = PositionMath.Apply(new PositionState(-200m, 9.00m, -500m), TradeSide.Buy, 500m, 8.00m);

        // closes 200 × (8.00 − 9.00) × (−1) = +200.00 → realised −300.00; remaining 300 long at 8.00
        result.Should().Be(new PositionState(300m, 8.00m, -300m));
    }

    [Fact]
    public void Crossing_zero_does_not_blend_the_old_average_cost_into_the_new_side()
    {
        // A naive weighted average over all 600 would give a nonsense cost.
        // Only the 200 that remain open matter, and they were opened at 9.00.
        var result = PositionMath.Apply(new PositionState(400m, 10.80m, 0m), TradeSide.Sell, 600m, 9.00m);

        result.AverageCost.Should().Be(9.00m);
    }

    // Rounding: average cost and realised are rounded to six decimal places
    // using banker's rounding (MidpointRounding.ToEven) after each trade.

    public static TheoryData<decimal, decimal, decimal> AverageCostMidpoints => new()
    {
        // existing average, incoming price, expected. Both are exact
        // midpoints: 10.0000005 and 10.0000015. Half-to-even sends the first
        // down (0 is even) and the second up (2 is even). Away-from-zero
        // would send both up, so the first row is the one that catches it.
        { 10.000000m, 10.000001m, 10.000000m },
        { 10.000001m, 10.000002m, 10.000002m },
    };

    [Theory]
    [MemberData(nameof(AverageCostMidpoints))]
    public void Average_cost_rounds_half_to_even_at_six_decimal_places(
        decimal existingAverage, decimal incomingPrice, decimal expectedAverage)
    {
        var result = PositionMath.Apply(new PositionState(2m, existingAverage, 0m), TradeSide.Buy, 2m, incomingPrice);

        result.AverageCost.Should().Be(expectedAverage);
    }

    [Fact]
    public void Average_cost_with_a_repeating_decimal_is_rounded_not_truncated()
    {
        var result = PositionMath.Apply(new PositionState(3m, 10.00m, 0m), TradeSide.Buy, 4m, 11.00m);

        // 74 ÷ 7 = 10.571428571… → 10.571429 (truncation would give 10.571428)
        result.AverageCost.Should().Be(10.571429m);
    }

    [Fact]
    public void Realised_pnl_is_rounded_to_six_decimal_places()
    {
        var result = PositionMath.Apply(new PositionState(7m, 10.571429m, 0m), TradeSide.Sell, 0.5m, 11.00m);

        // 0.5 × (11.000000 − 10.571429) = 0.2142855 → 0.214286
        result.Should().Be(new PositionState(6.5m, 10.571429m, 0.214286m));
    }

    // Guards. The API validates first; these make the class safe from anywhere.

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_rejects_a_quantity_that_is_not_positive(int quantity)
    {
        var act = () => PositionMath.Apply(PositionState.Flat, TradeSide.Buy, quantity, 10m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_rejects_a_price_that_is_not_positive(int price)
    {
        var act = () => PositionMath.Apply(PositionState.Flat, TradeSide.Buy, 10m, price);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("price");
    }

    // Unrealised: (last_price − A) × N. Not stored, so no rounding rule.

    [Fact]
    public void Unrealised_pnl_on_a_long_is_positive_when_price_rises()
    {
        var unrealised = PositionMath.UnrealisedPnl(new PositionState(300m, 10.80m, 220m), lastPrice: 11.30m);

        // (11.30 − 10.80) × 300 = 150.00
        unrealised.Should().Be(150.00m);
    }

    [Fact]
    public void Unrealised_pnl_on_a_short_is_positive_when_price_falls()
    {
        var unrealised = PositionMath.UnrealisedPnl(new PositionState(-200m, 9.00m, -500m), lastPrice: 8.50m);

        // (8.50 − 9.00) × (−200) = +100.00
        unrealised.Should().Be(100.00m);
    }

    [Fact]
    public void Unrealised_pnl_on_a_flat_position_is_zero_at_any_price()
    {
        var unrealised = PositionMath.UnrealisedPnl(new PositionState(0m, 0m, -525m), lastPrice: 42m);

        unrealised.Should().Be(0m);
    }
}
