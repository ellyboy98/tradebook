using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeBook.Api.Persistence.Entities;

namespace TradeBook.Api.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(a => a.Code).HasColumnName("code").HasMaxLength(16);
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(a => a.BaseCurrency).HasColumnName("base_currency").HasColumnType("char(3)");
        builder.Property(a => a.OwnerSubject).HasColumnName("owner_subject").HasMaxLength(64);
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").HasPrecision(3);

        builder.HasIndex(a => a.Code).IsUnique();
        // Every authorised read filters by the caller's subject.
        builder.HasIndex(a => a.OwnerSubject);

        builder.HasData(SeedData.Accounts);
    }
}
