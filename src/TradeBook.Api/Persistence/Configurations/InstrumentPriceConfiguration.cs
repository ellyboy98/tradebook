using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence.Configurations;

public sealed class InstrumentPriceConfiguration : IEntityTypeConfiguration<InstrumentPrice>
{
    public void Configure(EntityTypeBuilder<InstrumentPrice> builder)
    {
        builder.ToTable("instrument_prices");
        // The instrument id is both primary key and foreign key: one row per
        // instrument, and the value comes from the instrument, not an identity.
        builder.HasKey(p => p.InstrumentId);

        builder.Property(p => p.InstrumentId).HasColumnName("instrument_id").ValueGeneratedNever();
        builder.Property(p => p.LastPrice).HasColumnName("last_price").HasPrecision(18, 6);
        builder.Property(p => p.AsOfUtc).HasColumnName("as_of_utc").HasPrecision(3);

        builder.HasOne(p => p.Instrument)
            .WithOne()
            .HasForeignKey<InstrumentPrice>(p => p.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(SeedData.InstrumentPrices);
    }
}
