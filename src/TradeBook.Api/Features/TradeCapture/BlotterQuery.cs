using System.ComponentModel.DataAnnotations;

namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// Query string of <c>GET /api/trades</c> (design.md section 7). Bound from
/// the query string by MVC; the attributes and <see cref="Validate"/> are the
/// request-shape validation, so the handler only sees well-formed input.
/// </summary>
public sealed record BlotterQuery : IValidatableObject
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    /// <summary>Nullable so that "missing" is a 400 naming the field rather than account 0.</summary>
    [Required]
    public int? AccountId { get; init; }

    public int? InstrumentId { get; init; }

    /// <summary>Inclusive lower bound on execution time.</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Exclusive upper bound on execution time, so consecutive windows do not overlap.</summary>
    public DateTimeOffset? ToUtc { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromUtc is not null && ToUtc is not null && FromUtc > ToUtc)
        {
            yield return new ValidationResult("fromUtc must not be after toUtc.", [nameof(FromUtc), nameof(ToUtc)]);
        }
    }
}
