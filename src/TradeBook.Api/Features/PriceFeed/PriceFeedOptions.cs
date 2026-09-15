using System.ComponentModel.DataAnnotations;

namespace TradeBook.Api.Features.PriceFeed;

/// <summary>The <c>PriceFeed</c> configuration section (ADR-014).</summary>
public sealed class PriceFeedOptions
{
    public const string SectionName = "PriceFeed";

    /// <summary>Off in the integration tests so seeded prices stay put.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Seconds between ticks. Design.md section 3 says one.</summary>
    [Range(1, 3600)]
    public int IntervalSeconds { get; init; } = 1;

    /// <summary>
    /// Largest fractional move per tick. 0.002 means a price moves by at most
    /// 0.2% per second in either direction.
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal Volatility { get; init; } = 0.002m;
}
