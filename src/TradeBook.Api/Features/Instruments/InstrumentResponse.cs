using TradeBook.Api.Domain;

namespace TradeBook.Api.Features.Instruments;

/// <summary>
/// An instrument with its latest quote, when one exists. The quote is included
/// so the ticket can pre-fill a price for an instrument the account does not
/// yet hold.
/// </summary>
public sealed record InstrumentResponse(
    int Id,
    string Symbol,
    string Name,
    InstrumentType InstrumentType,
    string Currency,
    decimal TickSize,
    int LotSize,
    bool IsActive,
    decimal? LastPrice,
    DateTime? PricedAtUtc);
