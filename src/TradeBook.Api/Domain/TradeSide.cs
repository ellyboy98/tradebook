namespace TradeBook.Api.Domain;

/// <summary>
/// Direction of an execution. Stored as <c>tinyint</c>; the numeric values are
/// part of the schema contract (design.md section 4) and must not be renumbered.
/// </summary>
public enum TradeSide : byte
{
    Buy = 1,
    Sell = 2,
}
