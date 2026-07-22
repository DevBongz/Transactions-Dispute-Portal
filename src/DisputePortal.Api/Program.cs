using System.Text;
using DisputePortal.Api.BackgroundServices;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Auth;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Messaging;
using DisputePortal.Api.Observability;
using DisputePortal.Api.Repositories;
using DisputePortal.Api.Services;
using DisputePortal.Api.Services.Ai;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Two-stage Serilog init: a bootstrap logger captures startup failures before the
// host is built; UseSerilog() then swaps in the configured pipeline (TDP-OBS-01 §2.2).
SerilogConfiguration.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();

    // Swagger with a Bearer security definition so protected endpoints can be
    // exercised from the Swagger UI with a pasted token (TDP-AUTH-01 §4).
    builder.Services.AddSwaggerGen(o =>
    {
        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT from POST /api/v1/auth/login (no 'Bearer ' prefix).",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        o.AddSecurityDefinition("Bearer", scheme);
        o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });

    // EF Core / Npgsql — connection string injected by compose (SPEC §3.1).
    builder.Services.AddDbContext<DisputePortalDbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    // ---- JWT auth (TDP-AUTH-01) ----
    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

    // Fail fast on a missing/short secret so misconfiguration surfaces immediately
    // (SPEC §4.3 mitigation; HMAC-SHA256 needs >= 32 bytes).
    if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
        throw new InvalidOperationException(
            "Jwt:Secret must be set and at least 32 bytes. Set JWT_SECRET in .env (see .env.example).");

    builder.Services.AddSingleton(jwt);
    builder.Services.AddScoped<JwtTokenService>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)   // tight skew so 60-min expiry is honoured
            };
        });

    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy("Customer", p => p.RequireRole(nameof(UserRole.CUSTOMER)));
        o.AddPolicy("Ops", p => p.RequireRole(nameof(UserRole.OPS_ANALYST), nameof(UserRole.OPS_MANAGER)));
        o.AddPolicy("Manager", p => p.RequireRole(nameof(UserRole.OPS_MANAGER)));
        // Global fallback: every endpoint requires auth unless it opts out with [AllowAnonymous].
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    });

    // ---- CORS locked to the frontend origin (SPEC §3.6) ----
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:3000" };
    builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

    // ---- Observability (TDP-OBS-01) ----
    // Singleton so the singleton Kafka publisher can stamp the correlation id
    // (TDP-KAFKA-01): the accessor is a stateless wrapper over IHttpContextAccessor
    // (itself a singleton reading the per-request AsyncLocal context), so promoting
    // it from scoped is safe and avoids a captive-dependency at the publisher.
    builder.Services.AddSingleton<ICorrelationAccessor, HttpCorrelationAccessor>();
    builder.AddAppHealthChecks();

    // ---- Messaging: Kafka producer + domain events (TDP-KAFKA-01) ----
    builder.Services.AddKafkaProducer(builder.Configuration);

    // ---- Transactions (TDP-TXN-01): Controller -> Service -> Repository -> EF Core ----
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();

    // ---- Disputes (TDP-DISP-01/02/03) ----
    builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();
    builder.Services.AddScoped<IDisputeReferenceGenerator, DisputeReferenceGenerator>();
    builder.Services.AddScoped<IDisputeService, DisputeService>();

    // ---- AI services (TDP-AI-01/02/03) ----
    // Typed HttpClient for the Anthropic Messages API. The API key + version are pinned once
    // as default headers so the key is never serialized per-request or logged (SPEC §3.6).
    // Per-call timeouts are enforced inside the client via a linked CTS, so HttpClient.Timeout
    // is left generous and the shared client can serve all three features' budgets.
    builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
    builder.Services.AddHttpClient<IAnthropicClient, AnthropicClient>((sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
        http.BaseAddress = new Uri(opts.BaseUrl);
        http.Timeout = Timeout.InfiniteTimeSpan; // per-call timeout is enforced by the client
        http.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", opts.AnthropicVersion);
    });
    builder.Services.AddScoped<IDisputeExtractionService, DisputeExtractionService>();
    builder.Services.AddScoped<IDisputeClassificationService, DisputeClassificationService>();
    builder.Services.AddScoped<IResolutionSummaryService, ResolutionSummaryService>();

    // Background triage: consumes dispute.submitted, classifies, publishes dispute.classified (TDP-AI-02).
    builder.Services.AddHostedService<DisputeClassificationConsumer>();

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

    // ---- Middleware order (TDP-OBS-01 §2.3, TDP-AUTH-01 §2.5) ----
    // Correlation id first so it is present on request + auth-failure logs.
    app.UseMiddleware<CorrelationIdMiddleware>();
    // Exception boundary next so AppExceptions from controllers map to 404/409/400
    // (TDP-DISP-01 §2.6) while still being correlated and request-logged.
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseAppRequestLogging();

    app.UseCors("frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoints are public (no JWT) so probes work without auth.
    app.MapAppHealthChecks();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DisputePortal.Api terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory<Program> integration tests (TDP-TEST-01).
public partial class Program { }
