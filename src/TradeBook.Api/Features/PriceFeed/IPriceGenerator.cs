namespace TradeBook.Api.Features.PriceFeed;

/// <summary>
/// Produces the next price for an instrument. Behind an interface so the
/// synthetic generator can be swapped for a real feed (ADR-007) without
/// touching the tick or the hub.
/// </summary>
public interface IPriceGenerator
{
    /// <param name="lastPrice">The previous price, greater than zero.</param>
    /// <param name="tickSize">The instrument's minimum price increment; the result is a multiple of it.</param>
    decimal NextPrice(decimal lastPrice, decimal tickSize);
}
