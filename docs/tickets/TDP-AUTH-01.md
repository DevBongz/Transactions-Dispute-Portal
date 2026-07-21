# TDP-AUTH-01 — JWT Authentication & Role-Based Authorization

**Jira summary:** Implement self-contained JWT authentication and role-based authorization for the Transactions Dispute Portal. Deliver the public `POST /api/v1/auth/login` endpoint (bcrypt password verification, 60-minute JWT issuance), the `POST /api/v1/auth/logout` endpoint, JWT bearer middleware protecting all other endpoints, authorization policies for the three roles (`CUSTOMER`, `OPS_ANALYST`, `OPS_MANAGER`), and a CORS policy locked to the frontend origin. This fulfils SPEC AC-AUTH-01 and the SPEC §3.6 security NFRs, and is the gate every feature endpoint sits behind.

## 1. Context & Motivation

- **Background:** TDP-DATA-02 seeded users with bcrypt hashes and roles, but there is no way to authenticate or protect endpoints. SPEC §3.3 defines `/auth/login` returning `{ token, expiresAt, user }`, SPEC §2.3 AC-AUTH-01 sets the exact login behaviour, and SPEC §3.6 mandates JWT on every endpoint except login, bcrypt (work factor ≥ 12), and CORS restricted to the known frontend origin.
- **Business Impact:** Auth is the precondition for every other feature (TXN, DISP, AI, dashboard) — no ticket in Groups B–D can be exercised without it. It also directly satisfies the AUTH-01/02/03 user stories (SPEC §2.1) and protects customer financial data (transactions, disputes), which is non-negotiable in a banking context.
- **User Story:** As a customer or ops user, I want to log in with my credentials and receive a time-limited token that authorises only the actions my role permits, so that my account and data are protected and I can access exactly the parts of the portal my role allows.
- **Dependencies:** **TDP-DATA-01** (User entity) and **TDP-DATA-02** (seeded users with bcrypt hashes). `Jwt__Secret` is injected by **TDP-INFRA-02**. Consumed by every protected endpoint (TXN, DISP, AI, dashboard) and by the frontend auth context (TDP-FE-01). Maps to **Milestone Day 1 — Foundation** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Configuration binding

`appsettings.json` declares JWT and CORS config; secrets come from environment (compose injects `Jwt__Secret`, SPEC §3.1):

```json
{
  "Jwt": {
    "Issuer": "dispute-portal",
    "Audience": "dispute-portal-clients",
    "Secret": "",
    "ExpiryMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  }
}
```

Bind to a strongly-typed options record `Infrastructure/Auth/JwtOptions.cs`:

```csharp
public sealed class JwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Secret { get; set; } = default!;
    public int ExpiryMinutes { get; set; } = 60;
}
```

### 2.2 Fail-fast secret validation

On startup, reject an empty or too-short `Jwt:Secret` (must be ≥ 32 bytes for HMAC-SHA256) so misconfiguration surfaces immediately — mitigating the SPEC §4.3 "JWT secret misconfiguration" risk:

```csharp
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
    throw new InvalidOperationException(
        "Jwt:Secret must be set and at least 32 bytes. Set JWT_SECRET in .env (see .env.example).");
builder.Services.AddSingleton(jwt);
```

### 2.3 Token generation service — `Infrastructure/Auth/JwtTokenService.cs`

Issues a 60-minute HS256 token whose claims carry the user id, email, name, and role. `role` uses `ClaimTypes.Role` so ASP.NET Core role policies work out of the box.

```csharp
public sealed class JwtTokenService(JwtOptions options)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())   // CUSTOMER | OPS_ANALYST | OPS_MANAGER
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer, audience: options.Audience,
            claims: claims, expires: expiresAt.UtcDateTime, signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
```

### 2.4 Login endpoint — `Controllers/AuthController.cs`

`POST /api/v1/auth/login` is `[AllowAnonymous]`. It looks up the user by email, verifies the password with bcrypt, and returns the token payload from SPEC §3.3. On any failure it returns a **generic** 401 (no credential enumeration, AC-AUTH-01).

```csharp
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    DisputePortalDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == req.Email);

        // Verify even on missing user? Simpler: single generic failure path.
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var (token, expiresAt) = tokens.Create(user);
        return Ok(new LoginResponse(token, expiresAt,
            new UserDto(user.Id, user.FullName, user.Role.ToString())));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => NoContent(); // stateless: client discards token
}

public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(Guid Id, string FullName, string Role);
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
```

Example request/response (SPEC §3.3):

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "maya@example.com", "password": "Password123!" }
```

```json
200 OK
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-07-21T13:45:00+00:00",
  "user": { "id": "11111111-1111-1111-1111-111111111111", "fullName": "Maya Naidoo", "role": "CUSTOMER" }
}
```

Invalid credentials:

```json
401 Unauthorized
{ "error": "Invalid email or password." }
```

### 2.5 Authentication + authorization wiring — `Program.cs`

```csharp
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
    // Global fallback: every endpoint requires auth unless it opts out with [AllowAnonymous]
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
```

Middleware order (must be exactly this):

```csharp
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### 2.6 CORS policy

Locked to the configured frontend origin (SPEC §3.6: "CORS restricted to known frontend origin"). Origins come from `Cors:AllowedOrigins`, which compose sets to `http://localhost:3000`:

```csharp
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
              ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
```

### 2.7 Applying policies to feature endpoints (contract for later tickets)

The `FallbackPolicy` means everything requires a valid JWT by default. Feature tickets add role gates:

```csharp
[Authorize(Policy = "Customer")]                     // customer-only, e.g. POST /disputes (TDP-DISP-01)
[Authorize(Policy = "Ops")]                          // ops-only, e.g. PATCH /disputes/{id}/status, resolve (TDP-DISP-02/03)
[Authorize(Policy = "Manager")]                      // manager-only, e.g. dashboard summary (TDP-DISP/OPS)
[AllowAnonymous]                                     // only /auth/login
```

The authenticated user id is read from the `sub` claim (`User.FindFirstValue(JwtRegisteredClaimNames.Sub)`) so customer-scoped queries (e.g. "list caller's transactions", TXN-01) filter by the token subject rather than a client-supplied id.

## 3. Acceptance Criteria (AC-AUTH-01, SPEC §2.3)

- Given valid credentials, `POST /api/v1/auth/login` returns 200 with a JWT whose expiry is exactly 60 minutes from issuance, plus `expiresAt` and the `user` object `{ id, fullName, role }` (SPEC §3.3).
- Given invalid credentials (wrong password or unknown email), the endpoint returns HTTP 401 with a single generic error message — no distinction that reveals whether the email exists (no credential enumeration).
- Given an expired JWT, every protected endpoint returns HTTP 401.
- Given a missing or malformed `Authorization` header, protected endpoints return 401.
- `/auth/login` is reachable without a token; all other endpoints require a valid bearer token (enforced by the global fallback policy, SPEC §3.6).
- Role policies enforce access: a `CUSTOMER` token is rejected (403) from ops-only endpoints; an ops token is rejected from customer-only endpoints as appropriate.
- Passwords are verified against bcrypt hashes (work factor ≥ 12 from TDP-DATA-02); no plaintext comparison anywhere (SPEC §3.6).
- CORS allows the configured frontend origin and rejects others; preflight (`OPTIONS`) succeeds for `http://localhost:3000`.
- Startup fails fast with a clear message if `Jwt:Secret` is empty or < 32 bytes (SPEC §4.3 mitigation).

## 4. Technical Notes

- **Generic 401 message:** do not branch the error text on "user not found" vs "bad password". A single message prevents email enumeration (AC-AUTH-01). Note a minor timing side-channel exists (skipping bcrypt when the user is missing); acceptable for this scope, but a hardened version would verify against a dummy hash to equalise timing.
- **`ClockSkew`:** default is 5 minutes, which would let a token live ~65 minutes. Set to 30s so the 60-minute expiry (AC-AUTH-01) is honoured tightly.
- **Role claim mapping:** JwtBearer maps `ClaimTypes.Role` for `RequireRole`. Emit the role using the enum name (`user.Role.ToString()`), matching the policy `RequireRole(nameof(UserRole.OPS_ANALYST))` etc.
- **401 vs 403:** unauthenticated → 401 (no/expired/invalid token); authenticated but wrong role → 403. The fallback policy yields 401 for anonymous calls to protected routes automatically.
- **Logout is stateless:** JWT is self-contained (SPEC §1.2 "self-contained, embedded"); there is no server-side session to invalidate, so logout is client-side token discard returning 204 (SPEC §3.3). Session expiry (AUTH-03) is handled purely by token lifetime.
- **Secret source:** `JWT_SECRET` flows env → `Jwt__Secret` config key → `JwtOptions.Secret`. Never hard-code it; `.env.example` documents the requirement (TDP-INFRA-02).
- **Swagger auth:** add a Bearer security definition in Swagger (coordinated with TDP-DOC-01) so protected endpoints can be exercised from the Swagger UI with a pasted token.
- **CORS + credentials:** since auth uses a bearer header (not cookies), `AllowCredentials()` is not required; `WithOrigins(...).AllowAnyHeader().AllowAnyMethod()` is sufficient and avoids the wildcard-with-credentials pitfall.

## 5. Definition of Done

- [ ] `JwtOptions`, `JwtTokenService`, and fail-fast secret validation implemented.
- [ ] `POST /api/v1/auth/login` returns the SPEC §3.3 payload on success and a generic 401 on failure; bcrypt verification used.
- [ ] `POST /api/v1/auth/logout` returns 204 for an authenticated caller.
- [ ] JWT bearer authentication + authorization wired with correct middleware order; `Customer`, `Ops`, `Manager` policies and a global authenticated fallback policy registered.
- [ ] CORS policy `frontend` restricted to the configured origin(s).
- [ ] Unit tests: valid/expired/missing token behaviour and bcrypt verify (feeds TDP-TEST-01's "JWT middleware" cases, SPEC §4.4).
- [ ] Manual check: seeded `maya@example.com` / ops accounts log in and receive role-appropriate tokens; expired token yields 401.
- [ ] Startup rejects an empty/short `Jwt:Secret` with a clear error.
- [ ] Reviewed and merged; unblocks all protected feature endpoints and TDP-FE-01 auth context.
