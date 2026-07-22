using System.Security.Claims;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace DisputePortal.Api.Observability;

/// <summary>
/// Serilog wiring for the API (TDP-OBS-01 §2.2/§2.4): compact JSON to stdout with
/// standard enrichers, and per-request summary logging (method, path, status,
/// duration) with correlation id flowing via <see cref="Serilog.Context.LogContext"/>.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Bootstrap logger created before <c>builder.Build()</c> so startup failures
    /// are logged. <see cref="AddSerilogLogging"/> later swaps in the host-integrated
    /// pipeline (reading overrides from <c>appsettings.json</c>).
    /// </summary>
    public static void CreateBootstrapLogger() =>
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "DisputePortal.Api")
            .WriteTo.Console(new CompactJsonFormatter())
            .CreateBootstrapLogger();

    /// <summary>Attach the configured Serilog pipeline to the host.</summary>
    public static void AddSerilogLogging(this WebApplicationBuilder builder) =>
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "DisputePortal.Api")
            .WriteTo.Console(new CompactJsonFormatter()));

    /// <summary>One structured summary line per request, with correlation id + UserId.</summary>
    public static void UseAppRequestLogging(this WebApplication app) =>
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
            opts.EnrichDiagnosticContext = (diag, http) =>
            {
                diag.Set("RequestHost", http.Request.Host.Value ?? "-");
                diag.Set("UserId", http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
                // CorrelationId already flows via LogContext from CorrelationIdMiddleware.
            };
        });
}
