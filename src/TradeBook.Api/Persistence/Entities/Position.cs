namespace TradeBook.Api.Persistence.Entities;

/// <summary>
/// Derived holding for one account and instrument. A cached projection of the
/// trade history that exists for query speed (ADR-001). The only code allowed
/// to set <see cref="NetQuantity"/>, <see cref="AverageCost"/> or
/// <see cref="RealisedPnl"/> is the trade capture handler applying the output
/// of <c>PositionMath</c>.
/// </summary>
public sealed class Position
{
    public int Id { get; set; }

    public required int AccountId { get; set; }

    public required int InstrumentId { get; set; }

    /// <summary>Signed. Positive is long, negative is short, zero is flat.</summary>
    public decimal NetQuantity { get; set; }

    /// <summary>Weighted average cost of the open quantity. Zero when flat.</summary>
    public decimal AverageCost { get; set; }

    /// <summary>Accumulates over the life of the position and is never reset.</summary>
    public decimal RealisedPnl { get; set; }

    public long? LastTradeId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// SQL Server <c>rowversion</c>, mapped with <c>IsRowVersion()</c>. SQL Server
    /// assigns it on every insert and update; EF Core puts the value it last
    /// read into the WHERE clause of its UPDATE, which is how a concurrent
    /// change is detected (design.md section 6). Never set it by hand. The
    /// property is non-nullable so the column is NOT NULL; the empty default
    /// is never written because the value is always store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public Account Account { get; set; } = null!;

    public Instrument Instrument { get; set; } = null!;

    public Trade? LastTrade { get; set; }
}
