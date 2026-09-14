namespace TradeBook.Api.Domain;

/// <summary>
/// Weighted-average-cost position arithmetic, design.md section 5. Pure: no
/// database, no clock, no state. Given a position and one execution it returns
/// the next position, and that is all it does, which is why it can be tested
/// line by line against the worked example.
/// </summary>
public static class PositionMath
{
    /// <summary>Stored precision of average cost and realised P&amp;L: decimal(18,6).</summary>
    private const int Scale = 6;

    /// <summary>
    /// Applies one execution to <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The position before the trade.</param>
    /// <param name="side">Buy or Sell. The stored quantity is always positive; the sign comes from here.</param>
    /// <param name="quantity">Executed quantity, greater than zero.</param>
    /// <param name="price">Execution price, greater than zero.</param>
    public static PositionState Apply(PositionState state, TradeSide side, decimal quantity, decimal price)
    {
        // The API rejects these before we get here. The guards make the class
        // safe to call from anywhere else, and a bad argument fails loudly
        // rather than producing a plausible-looking wrong position.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        // Notation from the design document: q is the signed trade quantity,
        // N the net quantity before the trade, A the average cost, R realised.
        var q = side == TradeSide.Buy ? quantity : -quantity;
        var n = state.NetQuantity;
        var a = state.AverageCost;
        var r = state.RealisedPnl;

        // Opening from flat (N = 0):  N' = q,  A' = p,  R' = R
        if (n == 0m)
        {
            return new PositionState(q, Round(price), r);
        }

        // Increasing (N and q have the same sign):
        //   N' = N + q,  A' = (|N|·A + |q|·p) ÷ |N'|,  R' = R
        // Nothing is closed, so nothing is realised; the cost is blended by size.
        if (Math.Sign(n) == Math.Sign(q))
        {
            var newNet = n + q;
            var blendedCost = (Math.Abs(n) * a + quantity * price) / Math.Abs(newNet);
            return new PositionState(newNet, Round(blendedCost), r);
        }

        // From here on the trade is on the opposite side, so some or all of the
        // position closes. Realised P&L is measured against the average cost of
        // what closes. Multiplying by sign(N) makes the same formula work for
        // shorts: a short closes by buying, and a lower buy price is a gain.

        // Reducing without crossing zero (|q| ≤ |N|):
        //   closed = |q|,  R' = R + closed·(p − A)·sign(N),  N' = N + q,  A' = A (or 0 when N' = 0)
        if (quantity <= Math.Abs(n))
        {
            var realised = Round(r + quantity * (price - a) * Math.Sign(n));
            var remaining = n + q;
            // An exact close leaves nothing open, so there is no cost to carry.
            var averageCost = remaining == 0m ? 0m : a;
            return new PositionState(remaining, averageCost, realised);
        }

        // Crossing zero (|q| > |N|):
        //   closed = |N|,  R' = R + closed·(p − A)·sign(N),  N' = N + q,  A' = p
        // The whole old position closes at this trade's price. What is left is
        // a brand-new position on the other side, opened by this trade, so its
        // cost is this trade's price. The old average cost is not blended in.
        var closedQuantity = Math.Abs(n);
        var realisedAfterClose = Round(r + closedQuantity * (price - a) * Math.Sign(n));
        return new PositionState(n + q, Round(price), realisedAfterClose);
    }

    /// <summary>
    /// Mark-to-market profit on the open quantity: (last_price − A) × N. The sign
    /// works for both directions because N is negative for a short.
    /// </summary>
    public static decimal UnrealisedPnl(PositionState state, decimal lastPrice)
        => (lastPrice - state.AverageCost) * state.NetQuantity;

    /// <summary>
    /// Banker's rounding to the stored precision, applied after every trade so
    /// the stored value and any later recomputation agree (design.md section 5,
    /// Rounding). ToEven avoids the upward drift that away-from-zero rounding
    /// would accumulate over many trades.
    /// </summary>
    private static decimal Round(decimal value) => Math.Round(value, Scale, MidpointRounding.ToEven);
}
