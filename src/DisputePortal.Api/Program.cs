using System.Text;
using DisputePortal.Api.BackgroundServices;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure;
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

    // Swagger + JWT Authorize button + XML action summaries (TDP-DOC-01).
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Transactions Dispute Portal API",
            Version = "v1",
            Description =
                "Customer & operations dispute management over the DMC Fin-Motion journal/settlement layer. " +
                "Customers view transactions, raise and track disputes; ops staff triage and resolve them. " +
                "AI features: NL dispute extraction, auto-classification, and resolution summaries (Google Gemini).",
            Contact = new OpenApiContact { Name = "Bongani Duma" }
        });

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

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlPath))
            o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

        o.SupportNonNullableReferenceTypes();
    });

    // EF Core / Npgsql — connection string injected by compose (SPEC §3.1). Normalised so a
    // managed-host postgres:// URL (Render/Railway/Heroku) is accepted as well as key-value form.
    builder.Services.AddDbContext<DisputePortalDbContext>(opt =>
        opt.UseNpgsql(NpgsqlConnectionString.Normalize(builder.Configuration.GetConnectionString("Default"))));

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
                // Zero skew keeps expiry deterministic for auth tests (TDP-TEST-01) and
                // honours the 60-minute token lifetime tightly in production.
                ClockSkew = TimeSpan.Zero
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

    // ---- Ops dashboard metrics (OPS-06) ----
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    // ---- AI services (TDP-AI-01/02/03) — Google Gemini ----
    // Typed HttpClient for Gemini generateContent. The API key is pinned once as
    // x-goog-api-key so it is never serialized per-request or logged (SPEC §3.6).
    // Per-call timeouts are enforced inside the client via a linked CTS.
    builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
    builder.Services.AddHttpClient<IAnthropicClient, GeminiClient>((sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
        http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = Timeout.InfiniteTimeSpan; // per-call timeout is enforced by the client
        // API key is attached per-request as ?key= (see GeminiClient) — not as a default header.
    });
    if (string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]))
    {
        Log.Warning("Gemini:ApiKey is not set — AI extract/classify/summary will return 502 until configured.");
    }
    builder.Services.AddScoped<IDisputeExtractionService, DisputeExtractionService>();
    builder.Services.AddScoped<IDisputeClassificationService, DisputeClassificationService>();
    builder.Services.AddScoped<IResolutionSummaryService, ResolutionSummaryService>();

    // Background triage: consumes dispute.submitted, classifies, publishes dispute.classified (TDP-AI-02).
    builder.Services.AddHostedService<DisputeClassificationConsumer>();

    var app = builder.Build();

    // Swagger in Development + Docker (SPEC §3.6). Not enabled in Production-by-name
    // cloud hosts unless ASPNETCORE_ENVIRONMENT=Docker (Render compose-style demos).
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(ui =>
        {
            ui.SwaggerEndpoint("/swagger/v1/swagger.json", "Dispute Portal API v1");
            ui.RoutePrefix = "swagger";
            ui.DocumentTitle = "Transactions Dispute Portal API";
            ui.DisplayRequestDuration();
        });
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
