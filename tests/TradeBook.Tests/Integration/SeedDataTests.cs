using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Domain;
using TradeBook.Api.Persistence;

namespace TradeBook.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SeedDataTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Two_demo_accounts_are_seeded_with_distinct_owners()
    {
        await using var dbContext = sqlServer.CreateDbContext();

        var accounts = await dbContext.Accounts.OrderBy(a => a.Id).ToListAsync();

        accounts.Select(a => a.Code).Should().Equal("EQ-DESK-1", "EQ-DESK-2");
        accounts.Select(a => a.OwnerSubject).Should().OnlyHaveUniqueItems();
        accounts.Should().OnlyContain(a => a.IsActive && a.BaseCurrency == "USD");
    }

    [Fact]
    public async Task Every_seeded_instrument_is_an_active_equity_with_a_starting_price()
    {
        await using var dbContext = sqlServer.CreateDbContext();

        var instruments = await dbContext.Instruments.OrderBy(i => i.Id).ToListAsync();
        var prices = await dbContext.InstrumentPrices.ToListAsync();

        instruments.Select(i => i.Symbol).Should().Equal("AAPL", "MSFT", "TSLA", "NVDA");
        instruments.Should().OnlyContain(i => i.IsActive && i.InstrumentType == InstrumentType.Equity);
        prices.Select(p => p.InstrumentId).Should().BeEquivalentTo(instruments.Select(i => i.Id));
        prices.Should().OnlyContain(p => p.LastPrice > 0);
    }

    [Fact]
    public async Task Trades_and_positions_start_empty()
    {
        await using var dbContext = sqlServer.CreateDbContext();

        (await dbContext.Trades.AnyAsync()).Should().BeFalse();
        (await dbContext.Positions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Timestamps_read_back_as_utc()
    {
        await using var dbContext = sqlServer.CreateDbContext();

        var account = await dbContext.Accounts.FirstAsync();

        account.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        account.CreatedAtUtc.Should().Be(SeedData.SeededAtUtc);
    }
}
