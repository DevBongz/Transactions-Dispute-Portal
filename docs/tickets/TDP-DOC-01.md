# TDP-DOC-01 — Swagger/OpenAPI Documentation

**Jira summary:** Enable and configure Swagger/OpenAPI documentation for the entire Transactions Dispute Portal backend using Swashbuckle. Every endpoint across Auth, Transactions, Disputes, AI, and Dashboard must be documented with XML-comment summaries, request/response schemas, and accurate status codes; JWT bearer authentication must be wired into Swagger UI so protected endpoints can be exercised interactively with an "Authorize" button. Swagger UI must be reachable at `http://localhost:5000/swagger` in the Docker environment, satisfying AC-NFR and the §3.6 documentation NFR.

## 1. Context & Motivation

- **Background:** All backend endpoints (Groups B and C) are implemented and prefixed `/api/v1` (§3.3). The spec lists Swagger/OpenAPI as an in-scope deliverable (§1.2), an NFR ("All API endpoints documented via Swagger", §3.6), a manual-QA checklist item ("Swagger UI lists all endpoints and shows correct schemas", §4.4), and an acceptance criterion (AC-NFR: "Swagger UI is accessible at http://localhost:5000/swagger").
- **Business Impact:** Swagger is the primary discoverability and manual-verification surface for a sole-contributor project; it lets a reviewer explore the API without Postman and lets the QA journeys in §4.4 be run against a live, authenticated spec. It is a graded/checklist deliverable.
- **User Story:** As a reviewer or developer, I want interactive, accurate API documentation with the ability to authenticate so that I can understand and exercise every endpoint without reading source code.
- **Dependencies:** TDP-TXN-01, TDP-DISP-01, TDP-DISP-02, TDP-DISP-03, TDP-AI-01, TDP-AI-03 (endpoints to document); TDP-AUTH-01 (JWT scheme mirrored in Swagger). Milestone: **Day 7 (22 Jul)**, though config lands as endpoints are built on Day 2.

## 2. Detailed Description

### 2.1 Packages

`src/DisputePortal.Api/DisputePortal.Api.csproj`:

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.7.3" />
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- suppress "missing XML comment" noise while still emitting the XML file -->
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

`<GenerateDocumentationFile>` emits `DisputePortal.Api.xml` at build; Swashbuckle reads it to surface `///` summaries in the UI.

### 2.2 `Program.cs` registration

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transactions Dispute Portal API",
        Version = "v1",
        Description = "Customer & operations dispute management over the DMC Fin-Motion journal/settlement layer. "
                    + "Customers view transactions, raise and track disputes; ops staff triage and resolve them. "
                    + "AI features: NL dispute extraction, auto-classification, and resolution summaries.",
        Contact = new OpenApiContact { Name = "Bongani Duma", Email = "bonganiduma@capitecbank.co.za" }
    });

    // --- JWT bearer auth in Swagger UI (mirrors TDP-AUTH-01) ---
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste the JWT returned by POST /api/v1/auth/login. Do NOT prefix with 'Bearer '.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });

    // --- XML comments ---
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    options.SupportNonNullableReferenceTypes();
    options.EnableAnnotations(); // for [SwaggerOperation]/[SwaggerResponse] if used
});
```

Middleware — enabled in Development **and** the Docker/Production runtime so AC-NFR (`/swagger` reachable at :5000) holds. The container maps host `5000` → container `8080` (§3.1), so inside the container Swagger is served on `8080` and reached externally at `http://localhost:5000/swagger`:

```csharp
// Serve Swagger in all environments used for the submission (Dev + Docker).
app.UseSwagger();                         // serves /swagger/v1/swagger.json
app.UseSwaggerUI(ui =>
{
    ui.SwaggerEndpoint("/swagger/v1/swagger.json", "Dispute Portal API v1");
    ui.RoutePrefix = "swagger";           // UI at /swagger
    ui.DocumentTitle = "Transactions Dispute Portal API";
    ui.DisplayRequestDuration();
});
```

> Because §3.6 says "Swagger UI enabled in Development and Docker environments", gate on an explicit config flag (e.g. `Swagger:Enabled`, defaulted true for `Development` and the `Docker` environment) rather than `if (app.Environment.IsDevelopment())`, so the compose stack exposes it.

### 2.3 XML documentation on controllers

Every action carries a `///` summary, documented parameters, and `[ProducesResponseType]` for each status code the endpoint can return. Example for the dispute submission endpoint (TDP-DISP-01):

```csharp
/// <summary>Submit a dispute against one of the caller's transactions.</summary>
/// <remarks>
/// Generates a reference in the format <c>DSP-YYYYMMDD-NNNNN</c>, persists the dispute as
/// <c>OPEN</c>, and publishes a <c>dispute.submitted</c> Kafka event. AI classification runs
/// asynchronously and updates category/priority shortly after (see AC-AI-02).
/// </remarks>
/// <param name="request">Transaction id, optional category, free-text description, optional AI-extracted fields.</param>
/// <response code="201">Dispute created. Returns id, reference, and status.</response>
/// <response code="400">Validation failed (e.g. missing transactionId or description).</response>
/// <response code="401">Missing or expired JWT.</response>
/// <response code="403">Transaction does not belong to the caller.</response>
/// <response code="409">An open dispute already exists for this transaction.</response>
[HttpPost]
[ProducesResponseType(typeof(DisputeCreatedResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public async Task<ActionResult<DisputeCreatedResponse>> Submit([FromBody] SubmitDisputeRequest request) { /* ... */ }
```

Apply the same treatment to every endpoint in §3.3:

| Group | Endpoint | Key documented responses |
|---|---|---|
| Auth | `POST /api/v1/auth/login` **[Public / AllowAnonymous]** | 200 (token, expiresAt, user), 401 generic (AC-AUTH-01) |
| Auth | `POST /api/v1/auth/logout` | 204 |
| Transactions | `GET /api/v1/transactions` | 200 paged `{items,total,page,pageSize}`; documents `page,pageSize,from,to,merchant` query params |
| Transactions | `GET /api/v1/transactions/{id}` | 200 `Transaction`, 404 |
| Disputes | `POST /api/v1/disputes` | 201/400/401/403/409 (above) |
| Disputes | `GET /api/v1/disputes` | 200 paged; `status,priority,category,page,pageSize` params |
| Disputes | `GET /api/v1/disputes/{id}` | 200 `DisputeDetail` (with timeline), 404 |
| Disputes | `PATCH /api/v1/disputes/{id}/status` **[ops]** | 200, 403, 404 |
| Disputes | `POST /api/v1/disputes/{id}/resolve` **[ops]** | 200 `Resolution`, 400 (notes < 20 chars), 403, 404 |
| AI | `POST /api/v1/ai/extract-dispute` | 200 extracted fields + confidence map, 400, 502 (AI upstream error) |
| AI | `POST /api/v1/ai/generate-summary` **[ops]** | 200 `{summary}`, 400, 403, 502 |
| Dashboard | `GET /api/v1/dashboard/summary` **[ops]** | 200 `{totalOpen,byPriority,byCategory,avgResolutionHours}` |

Mark `POST /auth/login` with `[AllowAnonymous]` and add a `IOperationFilter` (or `[SwaggerOperation]`) note so the UI does not imply it needs a token; all other operations inherit the global security requirement from §2.2.

### 2.4 DTO schema annotations

Annotate request/response DTOs so schemas are self-explanatory — enums render as their allowed values, and examples appear:

```csharp
public sealed record SubmitDisputeRequest
{
    /// <summary>Id of the transaction being disputed. Must belong to the caller.</summary>
    /// <example>3f9a1c2e-1b2c-4d5e-8a9b-0c1d2e3f4a5b</example>
    public required Guid TransactionId { get; init; }

    /// <summary>Optional pre-selected category. One of UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER.</summary>
    public string? Category { get; init; }

    /// <summary>Free-text description of the problem from the customer.</summary>
    /// <example>I was charged R450 twice at Shoprite on 14 July but I only shopped once.</example>
    public required string Description { get; init; }
}
```

Render enum members as strings globally so `status`, `category`, `priority`, and `outcome` show their names, not integers:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// mirror in AddSwaggerGen: options.UseAllOfToExtendReferenceSchemas();
```

## 3. Acceptance Criteria

- Swagger UI is reachable at `http://localhost:5000/swagger` when the stack runs via `docker compose up --build` (AC-NFR, §4.4 QA item).
- The raw OpenAPI document is served at `/swagger/v1/swagger.json` and is valid OpenAPI 3.0.
- Every endpoint in §3.3 (Auth, Transactions, Disputes, AI, Dashboard) appears in the UI with a summary, documented parameters, and accurate `[ProducesResponseType]` status codes.
- An **Authorize** button is present; pasting a JWT from `POST /api/v1/auth/login` and invoking a protected endpoint sends `Authorization: Bearer <token>` and returns a successful (non-401) response.
- `POST /api/v1/auth/login` is documented as public/anonymous and is callable without authorising first.
- Enum-valued fields (`status`, `category`, `priority`, `outcome`) render as their string values in schemas; request DTOs show example values.
- Building the API produces `DisputePortal.Api.xml`, and XML summaries are visible in the UI (not just method names).
- Swagger generation is controlled by config so it is on for Development and the Docker runtime, per §3.6.

## 4. Technical Notes

- **Port mapping:** the container listens on `8080`; compose maps `5000:8080` (§3.1). Do not hard-code `5000` inside the app — reference `/swagger` as a relative path so it works regardless of host port.
- **Do not gate on `IsDevelopment()` alone:** the Docker runtime commonly runs as `Production`. Use an explicit `Swagger:Enabled` flag (default true) or check for `Development`/`Docker` so AC-NFR holds inside compose.
- **Security scheme id must be `"Bearer"`** and consistent between `AddSecurityDefinition` and the `OpenApiReference` in the requirement, or the Authorize button will not attach the header.
- **No secrets in the spec:** the OpenAPI document must never embed `ANTHROPIC_API_KEY` or `Jwt:Secret` (§3.6). AI endpoints document only their request/response shapes; the key is server-side only.
- **`NoWarn 1591`** keeps the build clean while `GenerateDocumentationFile` is on; still aim for summaries on all public actions/DTOs.
- **Login 401 wording:** document the generic error to reflect AC-AUTH-01 (no credential enumeration) — do not describe distinct "user not found" vs "wrong password" responses.
- **Consistency with tests:** the response types documented here (`DisputeCreatedResponse`, `Resolution`, `ExtractDisputeResponse`) are the same DTOs asserted in TDP-TEST-01; keep names in sync.

## 5. Definition of Done

- [ ] Swashbuckle configured in `Program.cs` with API info, JWT bearer security definition + global requirement, and XML comments.
- [ ] `<GenerateDocumentationFile>` enabled; XML file included in the published/container output so summaries render in Docker.
- [ ] All §3.3 endpoints carry `///` summaries and `[ProducesResponseType]` attributes for every returned status code.
- [ ] Enum-as-string serialization applied and reflected in schemas; example values on key request DTOs.
- [ ] `/swagger` verified reachable at `http://localhost:5000/swagger` under `docker compose up --build`, and Authorize-then-call works against a protected endpoint.
- [ ] `swagger.json` validates as OpenAPI 3.0 and contains no secrets.
- [ ] README (TDP-DOC-02) references the Swagger URL; §4.4 QA checklist item passes.
- [ ] Code reviewed and merged to `main`.
