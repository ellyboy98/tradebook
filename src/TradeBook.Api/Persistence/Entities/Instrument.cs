using TradeBook.Api.Domain;

namespace TradeBook.Api.Persistence.Entities;

public sealed class Instrument
{
    public int Id { get; set; }

    public required string Symbol { get; set; }

    public required string Name { get; set; }

    public InstrumentType InstrumentType { get; set; } = InstrumentType.Equity;

    public required string Currency { get; set; }

    public required decimal TickSize { get; set; }

    public required int LotSize { get; set; }

    public bool IsActive { get; set; } = true;
}
