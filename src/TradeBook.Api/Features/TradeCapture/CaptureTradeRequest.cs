using System.ComponentModel.DataAnnotations;
using TradeBook.Api.Domain;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// Body of <c>POST /api/trades</c> (design.md section 7). <c>required</c> makes
/// System.Text.Json reject a body with a missing field before the controller
/// runs, which is how the request shape is validated. Business rules (positive
/// quantity, active instrument, not in the future) live in the handler.
/// </summary>
public sealed record CaptureTradeRequest
{
    public required int AccountId { get; init; }

    public required int InstrumentId { get; init; }

    /// <summary>"Buy" or "Sell" on the wire; see the JsonStringEnumConverter in Program.cs.</summary>
    public required TradeSide Side { get; init; }

    public required decimal Quantity { get; init; }

    public required decimal Price { get; init; }

    /// <summary>
    /// When the execution happened. An OMS supplies this; a manual ticket may
    /// leave it out, in which case the server uses its own clock, which also
    /// keeps a browser whose clock runs a few seconds fast from being told its
    /// trade is "in the future".
    /// DateTimeOffset rather than DateTime so "2026-09-15T02:31:00Z" and
    /// "2026-09-15T10:31:00+08:00" both mean one unambiguous instant. A
    /// DateTime would arrive with Kind = Local for the second form and be
    /// silently shifted. The handler stores <c>.UtcDateTime</c>.
    /// </summary>
    public DateTimeOffset? ExecutedAtUtc { get; init; }

    /// <summary>Optional idempotency key, typically the OMS reference. Matches trades.external_ref nvarchar(64).</summary>
    [StringLength(64)]
    public string? ExternalRef { get; init; }
}
