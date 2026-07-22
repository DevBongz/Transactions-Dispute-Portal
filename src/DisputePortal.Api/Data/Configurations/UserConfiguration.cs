using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        e.Property(x => x.Role).HasColumnName("role").HasMaxLength(50)
            .HasConversion<string>().IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
