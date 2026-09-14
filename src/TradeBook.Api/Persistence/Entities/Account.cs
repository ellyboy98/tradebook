namespace TradeBook.Api.Persistence.Entities;

public sealed class Account
{
    public int Id { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string BaseCurrency { get; set; }

    /// <summary>
    /// The Keycloak <c>sub</c> claim of the trader who owns this account.
    /// Every ownership check compares against this value.
    /// </summary>
    public required string OwnerSubject { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}
