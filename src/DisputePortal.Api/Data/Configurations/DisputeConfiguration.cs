using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Api.Data.Configurations;

public sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> e)
    {
        e.ToTable("disputes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(30).IsRequired();
        e.HasIndex(x => x.Reference).IsUnique();
        e.Property(x => x.TransactionId).HasColumnName("transaction_id").IsRequired();
        e.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().IsRequired();
        e.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).HasConversion<string?>();   // nullable
        e.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20).HasConversion<string?>();   // nullable
        e.Property(x => x.CustomerDescription).HasColumnName("customer_description").IsRequired();
        e.Property(x => x.ExtractedFieldsJson).HasColumnName("extracted_fields_json").HasColumnType("jsonb");
        e.Property(x => x.AssignedToId).HasColumnName("assigned_to_id");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // One Transaction -> zero or one Dispute (unique FK)
        e.HasOne(x => x.Transaction).WithOne(t => t.Dispute)
            .HasForeignKey<Dispute>(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.TransactionId).IsUnique();

        e.HasOne(x => x.Customer).WithMany(u => u.Disputes)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.AssignedTo).WithMany()
            .HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);

        // helpful indexes for ops filtering (OPS-01/02) and dashboard (OPS-06)
        e.HasIndex(x => x.Status);
        e.HasIndex(x => new { x.Priority, x.Status });
    }
}
