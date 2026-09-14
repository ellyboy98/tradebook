using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence.Configurations;

public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("trades");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(t => t.AccountId).HasColumnName("account_id");
        builder.Property(t => t.InstrumentId).HasColumnName("instrument_id");
        // TradeSide is an enum backed by byte, so EF Core maps it to tinyint (1 = Buy, 2 = Sell).
        builder.Property(t => t.Side).HasColumnName("side");
        // Precision must be explicit. SQL Server would otherwise default every
        // decimal to (18,2) and silently truncate.
        builder.Property(t => t.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(t => t.Price).HasColumnName("price").HasPrecision(18, 6);
        builder.Property(t => t.ExecutedAtUtc).HasColumnName("executed_at_utc").HasPrecision(3);
        builder.Property(t => t.ExternalRef).HasColumnName("external_ref").HasMaxLength(64);
        builder.Property(t => t.CapturedBySubject).HasColumnName("captured_by_subject").HasMaxLength(64);
        builder.Property(t => t.CapturedAtUtc).HasColumnName("captured_at_utc").HasPrecision(3);

        // The blotter orders by execution time.
        builder.HasIndex(t => t.ExecutedAtUtc);
        // Unique only where present. SQL Server treats NULLs as equal in a
        // unique index, so without the filter a second trade with no
        // external reference would be rejected.
        builder.HasIndex(t => t.ExternalRef).IsUnique().HasFilter("[external_ref] IS NOT NULL");

        // Nothing in TradeBook deletes rows. Restrict makes an accidental delete
        // of an account or instrument fail loudly rather than cascade through
        // the trade history.
        builder.HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Instrument).WithMany().HasForeignKey(t => t.InstrumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
