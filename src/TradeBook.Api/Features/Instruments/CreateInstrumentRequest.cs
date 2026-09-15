using System.ComponentModel.DataAnnotations;

namespace TradeBook.Api.Features.Instruments;

/// <summary>Body of <c>POST /api/instruments</c> (ops only).</summary>
public sealed record CreateInstrumentRequest
{
    /// <summary>Stored upper-cased and trimmed; unique.</summary>
    [Required]
    [StringLength(16, MinimumLength = 1)]
    public required string Symbol { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>ISO 4217 code, three upper-case letters.</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a three-letter ISO code such as USD.")]
    public required string Currency { get; init; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335", ErrorMessage = "Tick size must be greater than zero.")]
    public required decimal TickSize { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Lot size must be at least one.")]
    public required int LotSize { get; init; }

    /// <summary>
    /// Optional starting quote. The synthetic feed walks from the last price,
    /// so an instrument created without one is not quoted until it has one.
    /// </summary>
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335", ErrorMessage = "Initial price must be greater than zero.")]
    public decimal? InitialPrice { get; init; }
}
