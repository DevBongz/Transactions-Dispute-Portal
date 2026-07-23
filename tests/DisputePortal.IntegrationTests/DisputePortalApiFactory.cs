using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Messaging;
using DisputePortal.Api.Services.Ai;
using DisputePortal.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace DisputePortal.IntegrationTests;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<DisputePortalApiFactory>;

/// <summary>
/// Boots the real API against ephemeral Postgres 16 (Testcontainers), fakes Kafka + LLM
/// (TDP-TEST-01 §2.6).
/// </summary>
public sealed class DisputePortalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "test-secret-that-is-at-least-32-bytes-long!!";
    public const string JwtIssuer = "dispute-portal";
    public const string JwtAudience = "dispute-portal-clients";

    private PostgreSqlContainer? _db;

    public FakeEventPublisher Events { get; } = new();
    public StubAnthropicClient Llm { get; } = new();

    public async Task InitializeAsync()
    {
        _db = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("disputeportal")
            .WithUsername("dp_user")
            .WithPassword("dp_pass")
            .Build();
        await _db.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_db is not null)
            await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_db is null)
            throw new InvalidOperationException("PostgreSQL container was not started (InitializeAsync).");

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _db.GetConnectionString(),
                ["Jwt:Secret"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Gemini:ApiKey"] = "test-key",
                ["Kafka:BootstrapServers"] = "localhost:1",
                ["Kafka:EnableClassificationConsumer"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Events);

            services.RemoveAll<IAnthropicClient>();
            services.AddSingleton<IAnthropicClient>(Llm);

            // Do not run Kafka topic init or the classification consumer against a missing broker.
            services.RemoveAll<IHostedService>();
        });
    }

    public HttpClient CreateClientAs(Guid userId, string role, string? email = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(userId, role, email ?? $"{userId:N}@test.local"));
        return client;
    }

    public static string MintToken(Guid userId, string role, string email, DateTime? expiresUtc = null)
    {
        var expires = expiresUtc ?? DateTime.UtcNow.AddMinutes(60);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", "Test User"),
            new Claim(ClaimTypes.Role, role)
        };
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(Guid CustomerId, Guid TransactionId)> SeedCustomerWithTransactionAsync(
        decimal amount = 450m)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DisputePortalDbContext>();

        var customerId = Guid.NewGuid();
        var txnId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = customerId,
            Email = $"cust-{customerId:N}@example.com",
            FullName = "Test Customer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4),
            Role = UserRole.CUSTOMER,
            CreatedAt = now
        });
        db.Transactions.Add(new Transaction
        {
            Id = txnId,
            CustomerId = customerId,
            Reference = $"TXN-20260714-{Random.Shared.Next(10000, 99999)}",
            MerchantName = "Shoprite",
            MerchantCategory = "Grocery Stores",
            Amount = amount,
            Currency = "ZAR",
            TransactionDate = now.AddDays(-7),
            Status = TransactionStatus.SETTLED,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return (customerId, txnId);
    }

    public async Task<(Guid DisputeId, Guid CustomerId, Guid OpsId)> SeedOpenDisputeAsync()
    {
        var (customerId, txnId) = await SeedCustomerWithTransactionAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DisputePortalDbContext>();

        var opsId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = opsId,
            Email = $"ops-{opsId:N}@capitec.ops",
            FullName = "Test Analyst",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4),
            Role = UserRole.OPS_ANALYST,
            CreatedAt = now
        });
        db.Disputes.Add(new Dispute
        {
            Id = disputeId,
            Reference = $"DSP-20260714-{Random.Shared.Next(10000, 99999)}",
            TransactionId = txnId,
            CustomerId = customerId,
            Status = DisputeStatus.OPEN,
            Category = DisputeCategory.DUPLICATE_CHARGE,
            Priority = DisputePriority.HIGH,
            CustomerDescription = "Charged twice at Shoprite — please refund the duplicate.",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.DisputeEvents.Add(new DisputeEvent
        {
            Id = Guid.NewGuid(),
            DisputeId = disputeId,
            EventType = DisputeEventType.SUBMITTED,
            ActorId = customerId,
            Description = "Dispute submitted by customer.",
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return (disputeId, customerId, opsId);
    }
}
