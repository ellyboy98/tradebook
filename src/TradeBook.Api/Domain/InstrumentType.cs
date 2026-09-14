namespace TradeBook.Api.Domain;

/// <summary>
/// Reserved for multi-asset support. Version 1 only ever stores
/// <see cref="Equity"/>; the column exists so adding a second type later is a
/// data change, not a schema rewrite.
/// </summary>
public enum InstrumentType : byte
{
    Equity = 1,
}
