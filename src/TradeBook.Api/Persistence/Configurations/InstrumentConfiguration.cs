using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence.Configurations;

public sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(i => i.Symbol).HasColumnName("symbol").HasMaxLength(16);
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(128);
        // InstrumentType is an enum backed by byte, so EF Core maps it to tinyint.
        builder.Property(i => i.InstrumentType).HasColumnName("instrument_type");
        builder.Property(i => i.Currency).HasColumnName("currency").HasColumnType("char(3)");
        builder.Property(i => i.TickSize).HasColumnName("tick_size").HasPrecision(18, 6);
        builder.Property(i => i.LotSize).HasColumnName("lot_size");
        builder.Property(i => i.IsActive).HasColumnName("is_active");

        builder.HasIndex(i => i.Symbol).IsUnique();

        builder.HasData(SeedData.Instruments);
    }
}
