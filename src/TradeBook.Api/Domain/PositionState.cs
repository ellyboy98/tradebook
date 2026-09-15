namespace TradeBook.Api.Domain;

/// <summary>
/// Immutable snapshot of a position: the three numbers <see cref="PositionMath"/>
/// reads and returns. It is a <c>record</c> so two states with the same numbers
/// are equal, which is what the unit tests compare, and so nothing can change a
/// state after it is created; a new trade produces a new state.
/// </summary>
/// <param name="NetQuantity">Signed. Positive is long, negative is short, zero is flat.</param>
/// <param name="AverageCost">Weighted average cost of the open quantity. Zero when flat.</param>
/// <param name="RealisedPnl">Lifetime realised profit or loss. Never reset.</param>
public sealed record PositionState(decimal NetQuantity, decimal AverageCost, decimal RealisedPnl)
{
    /// <summary>The state before the first trade: nothing held, nothing realised.</summary>
    public static PositionState Flat { get; } = new(0m, 0m, 0m);
}
