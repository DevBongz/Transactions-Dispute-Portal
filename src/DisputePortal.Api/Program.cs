using DisputePortal.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core / Npgsql — connection string injected by compose (SPEC §3.1).
builder.Services.AddDbContext<DisputePortalDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Swagger is enabled in Development and Docker environments (SPEC §3.6 Documentation).
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Run migrations + seed inside a scope, before serving traffic (TDP-DATA-02 §2.3).
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<DisputePortalDbContext>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    // MigrateAsync with a small retry loop (Postgres readiness).
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Migration attempt {Attempt} failed; retrying in 3s", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    await DatabaseSeeder.SeedAsync(db, logger);
}

// Minimal health endpoints so the compose healthcheck (TDP-INFRA-02) can probe the
// container. Full observability/health wiring is owned by TDP-OBS-01.
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.MapControllers();
app.Run();

// Exposed for WebApplicationFactory<Program> integration tests (TDP-TEST-01).
public partial class Program { }
