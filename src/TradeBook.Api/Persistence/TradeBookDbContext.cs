using Microsoft.EntityFrameworkCore;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence;

public sealed class TradeBookDbContext(DbContextOptions<TradeBookDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Instrument> Instruments => Set<Instrument>();

    public DbSet<Trade> Trades => Set<Trade>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<InstrumentPrice> InstrumentPrices => Set<InstrumentPrice>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp in TradeBook is UTC. SQL Server's datetime2 carries no
        // offset, so values read back with Kind = Unspecified and would
        // serialise without the trailing Z. This stamps Utc on the way out of
        // the database. Column names and precision are still mapped explicitly
        // in each configuration; this only affects the in-memory Kind.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeBookDbContext).Assembly);
    }
}
