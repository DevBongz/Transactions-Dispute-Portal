using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Api.Data.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> e)
    {
        e.ToTable("transactions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Reference).IsUnique();
        e.Property(x => x.MerchantName).HasColumnName("merchant_name").HasMaxLength(255).IsRequired();
        e.Property(x => x.MerchantCategory).HasColumnName("merchant_category").HasMaxLength(100);
        e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
        e.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").HasDefaultValue("ZAR");
        e.Property(x => x.TransactionDate).HasColumnName("transaction_date").IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        e.HasOne(x => x.Customer).WithMany(u => u.Transactions)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
