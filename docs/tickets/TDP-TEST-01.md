# TDP-TEST-01 — Backend Unit & Integration Tests

**Jira summary:** Establish the backend automated test suite for the Transactions Dispute Portal API. This ticket delivers xUnit unit tests for the core service and infrastructure classes (`DisputeService`, `AiClassificationService`, `DisputeReferenceGenerator`, JWT middleware) and integration tests that exercise the real HTTP pipeline via `WebApplicationFactory<Program>` against an ephemeral PostgreSQL 16 database provisioned by Testcontainers. The suite gives us regression protection over the highest-risk flows — dispute submission, resolution, and AI extraction — and is the quality gate that TDP-CICD-01 will enforce on every push to `main`.

## 1. Context & Motivation

- **Background:** By Day 7 the backend (Groups B and C) is feature-complete: transaction listing (TDP-TXN-01), dispute submission and reference generation (TDP-DISP-01), status/detail (TDP-DISP-02), resolution (TDP-DISP-03), the Kafka producer (TDP-KAFKA-01), and the AI services — extraction (TDP-AI-01) and the classification consumer (TDP-AI-02). None of this is covered by automated tests yet. The spec (§4.4) mandates "Unit and integration tests for backend."
- **Business Impact:** The portal handles customer financial disputes; a silent regression in reference generation, the duplicate-dispute guard, or resolution persistence has direct customer and operational impact. Tests protect the measurable objectives in §1.1 (≥95% submission success, 100% classification coverage) and let a sole contributor (Bongani) refactor with confidence in the final polish window.
- **User Story:** As the developer, I want a fast, deterministic backend test suite so that I can verify core dispute logic and the HTTP contract on every change without manual smoke-testing.
- **Dependencies:** TDP-TXN-01, TDP-DISP-01, TDP-DISP-03, TDP-AI-02 (subjects under test). Also relies on TDP-DATA-01/02 (EF Core model + migrations), TDP-AUTH-01 (JWT), and TDP-KAFKA-01 (producer abstraction to fake). Milestone: **Day 7 (22 Jul) — Polish, Tests & README**. Feeds TDP-CICD-01.

## 2. Detailed Description

### 2.1 Test project layout

Add two test projects to the solution alongside `src/DisputePortal.Api`:

```
tests/
├── DisputePortal.UnitTests/
│   ├── DisputePortal.UnitTests.csproj
│   ├── Services/
│   │   ├── DisputeServiceTests.cs
│   │   └── AiClassificationServiceTests.cs
│   ├── Infrastructure/
│   │   └── DisputeReferenceGeneratorTests.cs
│   ├── Auth/
│   │   └── JwtMiddlewareTests.cs
│   └── Fakes/
│       ├── FakeEventPublisher.cs        // in-memory IEventPublisher
│       └── AnthropicHandlerStub.cs      // HttpMessageHandler stub
└── DisputePortal.IntegrationTests/
    ├── DisputePortal.IntegrationTests.csproj
    ├── DisputePortalApiFactory.cs        // WebApplicationFactory<Program> + Testcontainers
    ├── Fixtures/PostgresFixture.cs
    ├── DisputeEndpointsTests.cs
    ├── ResolveEndpointTests.cs
    └── AiExtractEndpointTests.cs
```

Register both in `DisputePortal.sln`. Target `net8.0`. Package references:

```xml
<!-- both projects -->
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="FluentAssertions" Version="6.12.1" />
<PackageReference Include="NSubstitute" Version="5.1.0" />
<!-- integration only -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.10.0" />
```

> `Program` must be reachable by the test assembly. Ensure `src/DisputePortal.Api/Program.cs` ends with `public partial class Program { }` (top-level statements) and add `<InternalsVisibleTo Include="DisputePortal.IntegrationTests" />` if internals are touched.

### 2.2 Unit tests — `DisputeService`

Covers §4.4 "happy path and duplicate dispute guard". `DisputeService` depends on the dispute/transaction repositories, `DisputeReferenceGenerator`, and `IEventPublisher` (the Kafka producer abstraction from TDP-KAFKA-01). Substitute all collaborators; assert on the returned reference, the persisted status (`OPEN`), the `SUBMITTED` `DisputeEvent`, and that exactly one `dispute.submitted` event is published.

```csharp
public class DisputeServiceTests
{
    private readonly IDisputeRepository _disputes = Substitute.For<IDisputeRepository>();
    private readonly ITransactionRepository _txns = Substitute.For<ITransactionRepository>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly DisputeReferenceGenerator _refGen = new(new FixedClock(new DateOnly(2026, 7, 14)));
    private readonly DisputeService _sut;

    public DisputeServiceTests() =>
        _sut = new DisputeService(_disputes, _txns, _refGen, _publisher, NullLogger<DisputeService>.Instance);

    [Fact]
    public async Task SubmitDisputeAsync_HappyPath_PersistsOpenDisputeAndPublishesSubmittedEvent()
    {
        var customerId = Guid.NewGuid();
        var txn = new Transaction { Id = Guid.NewGuid(), CustomerId = customerId, Amount = 450m };
        _txns.GetByIdAsync(txn.Id).Returns(txn);
        _disputes.ExistsOpenForTransactionAsync(txn.Id).Returns(false);

        var result = await _sut.SubmitDisputeAsync(
            new SubmitDisputeCommand(txn.Id, Category: null, Description: "Charged twice at Shoprite"),
            customerId);

        result.Reference.Should().MatchRegex(@"^DSP-\d{8}-\d{5}$");
        result.Status.Should().Be("OPEN");
        await _disputes.Received(1).AddAsync(Arg.Is<Dispute>(d =>
            d.Status == "OPEN" && d.CustomerId == customerId && d.Category == null));
        await _publisher.Received(1).PublishAsync("dispute.submitted",
            Arg.Is<DisputeSubmittedEvent>(e => e.DisputeId == result.Id));
    }

    [Fact]
    public async Task SubmitDisputeAsync_WhenOpenDisputeExistsForTransaction_ThrowsAndDoesNotPublish()
    {
        var customerId = Guid.NewGuid();
        var txn = new Transaction { Id = Guid.NewGuid(), CustomerId = customerId };
        _txns.GetByIdAsync(txn.Id).Returns(txn);
        _disputes.ExistsOpenForTransactionAsync(txn.Id).Returns(true);

        var act = () => _sut.SubmitDisputeAsync(
            new SubmitDisputeCommand(txn.Id, null, "duplicate raise"), customerId);

        await act.Should().ThrowAsync<DuplicateDisputeException>();
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task SubmitDisputeAsync_WhenTransactionBelongsToAnotherCustomer_ThrowsForbidden()
    {
        var txn = new Transaction { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() };
        _txns.GetByIdAsync(txn.Id).Returns(txn);

        var act = () => _sut.SubmitDisputeAsync(
            new SubmitDisputeCommand(txn.Id, null, "not mine"), Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
```

Adapt the exact command/result/exception type names to those introduced in TDP-DISP-01; the assertions above define the required behaviour.

### 2.3 Unit tests — `AiClassificationService` (mocked Anthropic)

Covers §4.4 "mock Anthropic HTTP client, verify category/priority mapping". Inject an `HttpClient` backed by a stub `HttpMessageHandler` so no real Anthropic call is made and the API key is never required. Verify: (a) a well-formed Claude response maps to the correct `category`/`priority`; (b) an HTTP failure or unparseable body yields the `CLASSIFICATION_FAILED` fallback (AC-AI-02) rather than throwing; (c) the request targets `claude-haiku-4-5-20251001`.

```csharp
public sealed class AnthropicHandlerStub : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public AnthropicHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(_responder(req));

    public static AnthropicHandlerStub Returning(string toolText) => new(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"content":[{"type":"text","text":{{JsonSerializer.Serialize(toolText)}}}]}""",
                Encoding.UTF8, "application/json")
        });
}

public class AiClassificationServiceTests
{
    [Theory]
    [InlineData("DUPLICATE_CHARGE", "HIGH")]
    [InlineData("UNAUTHORISED", "CRITICAL")]
    public async Task ClassifyAsync_MapsClaudeJsonToCategoryAndPriority(string category, string priority)
    {
        var body = $$"""{"category":"{{category}}","priority":"{{priority}}","rationale":"test"}""";
        var http = new HttpClient(AnthropicHandlerStub.Returning(body)) { BaseAddress = new Uri("https://api.anthropic.com") };
        var sut = new AiClassificationService(http, Options.Create(new AnthropicOptions { ApiKey = "test-key" }),
                                              NullLogger<AiClassificationService>.Instance);

        var result = await sut.ClassifyAsync(new ClassificationContext("Shoprite", 450m, DateTime.UtcNow, "Grocery", "charged twice", 0));

        result.Category.Should().Be(category);
        result.Priority.Should().Be(priority);
    }

    [Fact]
    public async Task ClassifyAsync_WhenAnthropicReturns500_ReturnsClassificationFailedFallback()
    {
        var http = new HttpClient(new AnthropicHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)))
            { BaseAddress = new Uri("https://api.anthropic.com") };
        var sut = new AiClassificationService(http, Options.Create(new AnthropicOptions { ApiKey = "k" }),
                                              NullLogger<AiClassificationService>.Instance);

        var result = await sut.ClassifyAsync(new ClassificationContext("M", 10m, DateTime.UtcNow, "C", "text", 0));

        result.Failed.Should().BeTrue();   // caller sets Dispute.Status = CLASSIFICATION_FAILED
    }
}
```

### 2.4 Unit tests — `DisputeReferenceGenerator`

Covers §4.4 "format validation". Reference format is `DSP-YYYYMMDD-NNNNN` (§3.2 / AC-DISP-04). Inject a deterministic clock and a sequence source so tests are hermetic.

```csharp
public class DisputeReferenceGeneratorTests
{
    [Fact]
    public void Generate_ProducesSpecFormat()
    {
        var gen = new DisputeReferenceGenerator(new FixedClock(new DateOnly(2026, 7, 14)));
        gen.Generate(sequence: 42).Should().Be("DSP-20260714-00042");
    }

    [Fact]
    public void Generate_ZeroPadsSequenceToFiveDigits() =>
        new DisputeReferenceGenerator(new FixedClock(new DateOnly(2026, 1, 5)))
            .Generate(1).Should().Be("DSP-20260105-00001");

    [Fact]
    public void Generate_AlwaysMatchesReferenceRegex() =>
        new DisputeReferenceGenerator(new FixedClock(new DateOnly(2026, 12, 31)))
            .Generate(99999).Should().MatchRegex(@"^DSP-\d{8}-\d{5}$");
}
```

### 2.5 Unit tests — JWT middleware

Covers §4.4 "valid token, expired token, missing token" and AC-AUTH-01. Prefer testing the configured `JwtBearer` behaviour through a minimal in-memory host over a bespoke middleware, so the real token-validation parameters (issuer, audience, signing key, `ClockSkew`) from TDP-AUTH-01 are exercised.

```csharp
public class JwtMiddlewareTests
{
    private static HttpClient BuildClient(string secret) =>
        new TestServer(new WebHostBuilder()
            .ConfigureServices(s =>
            {
                s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                 .AddJwtBearer(o => JwtSetup.Configure(o, secret)); // shared config from Api
                s.AddAuthorization();
                s.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(e => e.MapGet("/secure", () => "ok").RequireAuthorization());
            })).CreateClient();

    [Fact]
    public async Task ValidToken_Returns200()
    {
        var client = BuildClient(TestTokens.Secret);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(60)));
        (await client.GetAsync("/secure")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var client = BuildClient(TestTokens.Secret);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(-1)));
        (await client.GetAsync("/secure")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingToken_Returns401() =>
        (await BuildClient(TestTokens.Secret).GetAsync("/secure"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

`ClockSkew` must be set to `TimeSpan.Zero` in `JwtSetup.Configure` for the expired-token test to be deterministic.

### 2.6 Integration test harness — `WebApplicationFactory<Program>` + Testcontainers PostgreSQL

A single collection fixture starts one PostgreSQL 16 container for the whole assembly. The factory overrides the `ConnectionStrings:Default` config, swaps the real Kafka `IEventPublisher` for an in-memory fake, and stubs the Anthropic `HttpClient` so integration tests need no external services (§4.4 "mock Anthropic in integration env").

```csharp
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<DisputePortalApiFactory> { }

public class DisputePortalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("disputeportal").WithUsername("dp_user").WithPassword("dp_pass")
        .Build();

    public FakeEventPublisher Events { get; } = new();

    public async Task InitializeAsync() => await _db.StartAsync();
    public new async Task DisposeAsync() => await _db.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _db.GetConnectionString(),
            ["Anthropic:ApiKey"] = "test-key",
            ["Jwt:Secret"] = TestTokens.Secret
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Events);          // captures published events
            services.RemoveAll<IHostedService>();                    // do not run the Kafka consumer
            services.AddHttpClient("anthropic")                      // deterministic AI responses
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        AnthropicHandlerStub.Returning("""{"merchantName":"Shoprite","amount":450,"category":"DUPLICATE_CHARGE","confidence":{"amount":0.9}}"""));
        });
    }
}
```

`Program` runs `db.Database.MigrateAsync()` on startup (§3.6 Reliability), so the container schema is created automatically the first time the factory boots. Seed a known customer + transaction per test via a helper that opens a scope on `factory.Services` and writes through `DisputePortalDbContext`.

### 2.7 Integration test — `POST /api/v1/disputes`

Per §4.4: assert 201, reference format, and Kafka message published (AC-DISP-04).

```csharp
[Collection("api")]
public class DisputeEndpointsTests
{
    private readonly DisputePortalApiFactory _factory;
    public DisputeEndpointsTests(DisputePortalApiFactory f) => _factory = f;

    [Fact]
    public async Task PostDisputes_WithValidTransaction_Returns201WithReferenceAndPublishesSubmittedEvent()
    {
        var (customerId, txnId) = await _factory.SeedCustomerWithTransactionAsync(amount: 450m);
        var client = _factory.CreateClientAs(customerId, role: "CUSTOMER");

        var resp = await client.PostAsJsonAsync("/api/v1/disputes",
            new { transactionId = txnId, description = "Charged R450 twice at Shoprite on 14 July." });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<DisputeCreatedResponse>();
        body!.Reference.Should().MatchRegex(@"^DSP-\d{8}-\d{5}$");
        body.Status.Should().Be("OPEN");

        _factory.Events.Published.Should().ContainSingle(e => e.Topic == "dispute.submitted");
    }

    [Fact]
    public async Task PostDisputes_WithoutJwt_Returns401()
    {
        var resp = await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/disputes", new { transactionId = Guid.NewGuid(), description = "x" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

`CreateClientAs` mints a JWT with the given `sub`/`role` using `TestTokens` and the shared secret so the real auth pipeline is exercised end-to-end.

### 2.8 Integration test — `POST /api/v1/disputes/{id}/resolve`

Per §4.4: assert resolution persisted and Kafka event published (AC-OPS-04/AC-AI-03).

```csharp
[Fact]
public async Task Resolve_AsOpsAnalyst_PersistsResolutionAndPublishesResolvedEvent()
{
    var (disputeId, _) = await _factory.SeedOpenDisputeAsync();
    var client = _factory.CreateClientAs(Guid.NewGuid(), role: "OPS_ANALYST");

    var resp = await client.PostAsJsonAsync($"/api/v1/disputes/{disputeId}/resolve", new
    {
        outcome = "UPHELD",
        internalNotes = "Transaction confirmed as duplicate — refund initiated via settlement-processor.",
        customerSummary = "We reviewed your dispute and refunded the duplicate R450 charge."
    });

    resp.StatusCode.Should().Be(HttpStatusCode.OK);

    await using var scope = _factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DisputePortalDbContext>();
    var dispute = await db.Disputes.Include(d => d.Resolution).SingleAsync(d => d.Id == disputeId);
    dispute.Status.Should().Be("RESOLVED");
    dispute.Resolution!.Outcome.Should().Be("UPHELD");
    _factory.Events.Published.Should().Contain(e => e.Topic == "dispute.resolved");
}

[Fact]
public async Task Resolve_AsCustomer_Returns403()
{
    var (disputeId, customerId) = await _factory.SeedOpenDisputeAsync();
    var client = _factory.CreateClientAs(customerId, role: "CUSTOMER");
    var resp = await client.PostAsJsonAsync($"/api/v1/disputes/{disputeId}/resolve",
        new { outcome = "UPHELD", internalNotes = new string('x', 25), customerSummary = "s" });
    resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### 2.9 Integration test — `POST /api/v1/ai/extract-dispute`

Per §4.4: assert response shape with Anthropic stubbed (AC-DISP-02). The stubbed handler from §2.6 returns a fixed extraction payload.

```csharp
[Fact]
public async Task ExtractDispute_ReturnsStructuredFieldsAndConfidenceMap()
{
    var client = _factory.CreateClientAs(Guid.NewGuid(), role: "CUSTOMER");
    var resp = await client.PostAsJsonAsync("/api/v1/ai/extract-dispute",
        new { text = "I was charged R450 twice at Shoprite on 14 July but I only shopped once." });

    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var dto = await resp.Content.ReadFromJsonAsync<ExtractDisputeResponse>();
    dto!.MerchantName.Should().Be("Shoprite");
    dto.Amount.Should().Be(450m);
    dto.Category.Should().Be("DUPLICATE_CHARGE");
    dto.Confidence.Should().ContainKey("amount");
}
```

### 2.10 Running

```bash
dotnet test DisputePortal.sln                    # whole suite
dotnet test tests/DisputePortal.UnitTests        # fast, no Docker
dotnet test tests/DisputePortal.IntegrationTests # requires Docker daemon (Testcontainers)
```

## 3. Acceptance Criteria

- Solution contains `DisputePortal.UnitTests` and `DisputePortal.IntegrationTests`, both building under `net8.0` and included in `DisputePortal.sln`.
- **Unit — DisputeService:** happy-path submission asserts `OPEN` status, spec-format reference, and exactly one `dispute.submitted` publish; the duplicate-dispute guard test asserts an exception is thrown and no event is published.
- **Unit — AiClassificationService:** Anthropic HTTP client is mocked (no live call, no real key); a valid response maps to the correct `category`/`priority`; an Anthropic failure yields the `CLASSIFICATION_FAILED` fallback without throwing (AC-AI-02); the request uses `claude-haiku-4-5-20251001`.
- **Unit — DisputeReferenceGenerator:** output matches `^DSP-\d{8}-\d{5}$`, date reflects an injected clock, and the sequence is zero-padded to five digits (AC-DISP-04).
- **Unit — JWT:** valid token → 200; expired token → 401; missing token → 401 (AC-AUTH-01), exercised through the real `JwtBearer` configuration with `ClockSkew = 0`.
- **Integration** tests boot via `WebApplicationFactory<Program>` against a Testcontainers `postgres:16-alpine` container; schema is created by the app's own `MigrateAsync()` on startup.
- `POST /api/v1/disputes` → 201, reference matches the format, and a `dispute.submitted` event is captured by the fake publisher; missing JWT → 401.
- `POST /api/v1/disputes/{id}/resolve` → 200, `Resolution` row persisted with the correct outcome, dispute status `RESOLVED`, `dispute.resolved` event captured; a `CUSTOMER` caller → 403.
- `POST /api/v1/ai/extract-dispute` → 200 with the documented response shape (`merchantName`, `amount`, `category`, `confidence` map), Anthropic stubbed.
- `dotnet test DisputePortal.sln` runs green locally with a Docker daemon available.

## 4. Technical Notes

- **`Program` reachability:** top-level `Program.cs` needs `public partial class Program { }` for `WebApplicationFactory<Program>` to bind; otherwise the factory cannot locate the entry point.
- **One container per assembly:** use an `ICollectionFixture` so PostgreSQL starts once, not per test — container startup dominates runtime. Do not run tests that mutate shared rows in parallel across collections; keep write-heavy tests in the single `"api"` collection (xUnit serialises within a collection).
- **No real Kafka/Anthropic in tests:** the factory removes the real `IEventPublisher` and all `IHostedService` registrations (so the TDP-AI-02 classification consumer does not spin up and try to reach a broker), and injects `test-key` for `Anthropic:ApiKey`. This keeps §3.6 security intact — no real key is ever needed to run tests.
- **Determinism:** inject an `IClock`/`FixedClock` into `DisputeReferenceGenerator` and set `TokenValidationParameters.ClockSkew = TimeSpan.Zero`. Avoid `DateTime.UtcNow` directly in the generator.
- **Testcontainers prerequisite:** a running Docker daemon. On CI (TDP-CICD-01) the `ubuntu-latest` runner provides Docker out of the box; document this so contributors without Docker can still run the unit project alone.
- **Cleanup between tests:** either respawn (truncate) tables via `Respawn` or scope each test to freshly seeded unique IDs. Prefer unique-ID seeding for speed; reserve truncation for the resolve/status tests that assert absence.
- **P95 target unaffected:** these tests are correctness-focused; performance NFRs (§3.6) are validated by manual QA, not asserted here.

## 5. Definition of Done

- [ ] Both test projects created, added to `DisputePortal.sln`, and restore/build cleanly on `net8.0`.
- [ ] All unit tests from §2.2–2.5 implemented and green (`DisputeService`, `AiClassificationService`, `DisputeReferenceGenerator`, JWT).
- [ ] Integration harness (`DisputePortalApiFactory`) provisions PostgreSQL via Testcontainers, overrides config, fakes the event publisher, and stubs Anthropic.
- [ ] Integration tests for `POST /disputes`, `POST /disputes/{id}/resolve`, and `POST /ai/extract-dispute` implemented and green, including the auth negative cases (401/403).
- [ ] `dotnet test DisputePortal.sln` passes locally end-to-end with Docker available.
- [ ] Test invocation commands documented in the README (coordinate with TDP-DOC-02) and the suite is wired into CI (TDP-CICD-01).
- [ ] Code reviewed and merged to `main`.
