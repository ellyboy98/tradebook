using System.ComponentModel.DataAnnotations;

namespace TradeBook.Api.Features.Accounts;

/// <summary>Body of <c>POST /api/accounts</c> (ops only).</summary>
public sealed record CreateAccountRequest
{
    /// <summary>Short unique code shown in the account rail, e.g. EQ-DESK-3.</summary>
    [Required]
    [StringLength(16, MinimumLength = 1)]
    public required string Code { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Base currency must be a three-letter ISO code such as USD.")]
    public required string BaseCurrency { get; init; }

    /// <summary>The Keycloak user id (<c>sub</c>) of the trader who owns the account.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string OwnerSubject { get; init; }
}
