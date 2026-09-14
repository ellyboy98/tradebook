using TradeBook.Api.Domain;

namespace TradeBook.Api.Persistence.Entities;

/// <summary>
/// One execution. Trades are the record of fact and are append-only
/// (ADR-001), so every property is <c>init</c>-only: once constructed, nothing
/// in the application can change a trade. Corrections are booked as offsetting
/// trades.
/// </summary>
public sealed class Trade
{
    public long Id { get; init; }

    public required int AccountId { get; init; }

    public required int InstrumentId { get; init; }

    public required TradeSide Side { get; init; }

    /// <summary>Always positive. Direction lives in <see cref="Side"/>.</summary>
    public required decimal Quantity { get; init; }

    public required decimal Price { get; init; }

    public required DateTime ExecutedAtUtc { get; init; }

    /// <summary>
    /// Caller-supplied idempotency key, typically the order management system's
    /// reference. Unique when present; a repeat submission returns the original.
    /// </summary>
    public string? ExternalRef { get; init; }

    public required string CapturedBySubject { get; init; }

    public required DateTime CapturedAtUtc { get; init; }

    public Account Account { get; init; } = null!;

    public Instrument Instrument { get; init; } = null!;
}
