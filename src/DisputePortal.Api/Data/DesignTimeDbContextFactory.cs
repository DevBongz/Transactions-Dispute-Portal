using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DisputePortal.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DisputePortalDbContext>
{
    public DisputePortalDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? "Host=localhost;Database=disputeportal;Username=dp_user;Password=dp_pass";
        var options = new DbContextOptionsBuilder<DisputePortalDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new DisputePortalDbContext(options);
    }
}
