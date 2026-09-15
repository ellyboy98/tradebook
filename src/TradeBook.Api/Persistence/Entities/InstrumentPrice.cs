namespace TradeBook.Api.Persistence.Entities;

/// <summary>
/// Latest price per instrument. One row per instrument, overwritten on every
/// tick. Kept in the database rather than memory only so positions value
/// correctly immediately after a restart.
/// </summary>
public sealed class InstrumentPrice
{
    public int InstrumentId { get; set; }

    public decimal LastPrice { get; set; }

    public DateTime AsOfUtc { get; set; }

    public Instrument Instrument { get; set; } = null!;
}
