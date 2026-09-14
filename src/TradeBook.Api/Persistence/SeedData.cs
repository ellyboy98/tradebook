using TradeBook.Api.Domain;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence;

/// <summary>
/// Demo data written by the initial migration through <c>HasData</c>.
/// </summary>
/// <remarks>
/// Every value here is a constant on purpose. EF Core compares seed rows
/// against the model snapshot when scaffolding a migration, so anything that
/// changes between runs (<c>DateTime.UtcNow</c>, <c>Guid.NewGuid()</c>) would
/// produce a spurious migration every time.
/// </remarks>
public static class SeedData
{
    /// <summary>
    /// Keycloak user ids, which become the JWT <c>sub</c> claim, for the two
    /// demo traders. The realm export in build step 7 must create its users
    /// with these exact ids or the ownership checks will never match.
    /// </summary>
    public const string Trader1Subject = "0f8fad5b-d9cb-469f-a165-70867728950e";

    public const string Trader2Subject = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    public static readonly DateTime SeededAtUtc = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Account[] Accounts =
    [
        new()
        {
            Id = 1,
            Code = "EQ-DESK-1",
            Name = "Equities Desk 1",
            BaseCurrency = "USD",
            OwnerSubject = Trader1Subject,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
        },
        new()
        {
            Id = 2,
            Code = "EQ-DESK-2",
            Name = "Equities Desk 2",
            BaseCurrency = "USD",
            OwnerSubject = Trader2Subject,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
        },
    ];

    public static readonly Instrument[] Instruments =
    [
        Equity(1, "AAPL", "Apple Inc."),
        Equity(2, "MSFT", "Microsoft Corp."),
        Equity(3, "TSLA", "Tesla Inc."),
        Equity(4, "NVDA", "NVIDIA Corp."),
    ];

    // Starting prices match prototypes/blotter.html. They are deliberately toy
    // numbers in the same range as the worked example in design.md section 5.
    public static readonly InstrumentPrice[] InstrumentPrices =
    [
        Price(1, 11.02m),
        Price(2, 8.50m),
        Price(3, 42.10m),
        Price(4, 27.35m),
    ];

    private static Instrument Equity(int id, string symbol, string name) => new()
    {
        Id = id,
        Symbol = symbol,
        Name = name,
        InstrumentType = InstrumentType.Equity,
        Currency = "USD",
        TickSize = 0.01m,
        LotSize = 1,
        IsActive = true,
    };

    private static InstrumentPrice Price(int instrumentId, decimal lastPrice) => new()
    {
        InstrumentId = instrumentId,
        LastPrice = lastPrice,
        AsOfUtc = SeededAtUtc,
    };
}
