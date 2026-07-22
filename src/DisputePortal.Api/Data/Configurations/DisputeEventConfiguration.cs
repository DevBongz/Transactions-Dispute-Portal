using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Api.Data.Configurations;

public sealed class DisputeEventConfiguration : IEntityTypeConfiguration<DisputeEvent>
{
    public void Configure(EntityTypeBuilder<DisputeEvent> e)
    {
        e.ToTable("dispute_events");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.DisputeId).HasColumnName("dispute_id").IsRequired();
        e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).HasConversion<string>().IsRequired();
        e.Property(x => x.ActorId).HasColumnName("actor_id");
        e.Property(x => x.Description).HasColumnName("description").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        e.HasOne(x => x.Dispute).WithMany(d => d.Events)
            .HasForeignKey(x => x.DisputeId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.DisputeId);
    }
}
