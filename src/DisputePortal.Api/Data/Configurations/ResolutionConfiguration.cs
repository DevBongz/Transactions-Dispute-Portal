using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Api.Data.Configurations;

public sealed class ResolutionConfiguration : IEntityTypeConfiguration<Resolution>
{
    public void Configure(EntityTypeBuilder<Resolution> e)
    {
        e.ToTable("resolutions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.DisputeId).HasColumnName("dispute_id").IsRequired();
        e.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(50).HasConversion<string>().IsRequired();
        e.Property(x => x.InternalNotes).HasColumnName("internal_notes").IsRequired();
        e.Property(x => x.CustomerSummary).HasColumnName("customer_summary");
        e.Property(x => x.ResolvedById).HasColumnName("resolved_by_id").IsRequired();
        e.Property(x => x.ResolvedAt).HasColumnName("resolved_at").IsRequired();

        e.HasOne(x => x.Dispute).WithOne(d => d.Resolution)
            .HasForeignKey<Resolution>(x => x.DisputeId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.DisputeId).IsUnique();
        e.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict);
    }
}
