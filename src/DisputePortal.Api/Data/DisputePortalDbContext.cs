using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Data;

public sealed class DisputePortalDbContext(DbContextOptions<DisputePortalDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvent> DisputeEvents => Set<DisputeEvent>();
    public DbSet<Resolution> Resolutions => Set<Resolution>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(DisputePortalDbContext).Assembly);
    }
}
