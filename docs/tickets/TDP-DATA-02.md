# TDP-DATA-02 — Database Migrations & Seed Data

**Jira summary:** Generate the initial EF Core migration for the `DisputePortalDbContext` schema, wire `MigrateAsync()` to run automatically on API startup, and implement idempotent seed data — demo users for each role (`CUSTOMER`, `OPS_ANALYST`, `OPS_MANAGER`) with bcrypt password hashes (work factor ≥ 12) and a realistic set of transactions for the demo customer. This makes a freshly-started stack (`docker compose up --build`) immediately usable: a reviewer can log in and see transactions with no manual DB setup, directly supporting the SPEC §4.4 manual-QA checklist and the AC-NFR Docker criteria.

## 1. Context & Motivation

- **Background:** TDP-DATA-01 defined the entities and `DbContext`, but the database is empty and has no schema. SPEC §3.6 (Reliability) and §4.3 (risk mitigation) require migrations to run automatically on startup via `MigrateAsync()`. SPEC §4.4's QA checklist requires working "seed customer and ops accounts" and a customer with visible transactions to demo the end-to-end journeys.
- **Business Impact:** Without seed data, a reviewer cannot log in at all — the whole graded demo (SPEC §4.4) is blocked. Auto-migration on startup mitigates the "EF Core migration issues on fresh DB" risk (SPEC §4.3) and underpins "runnable from a single command" (SPEC §1.1). Idempotent seeding lets `docker compose down -v && up --build` reproduce an identical, working state.
- **User Story:** As a reviewer, I want the database schema and demo accounts/transactions to be created automatically when the stack starts, so that I can immediately log in as a customer or ops user and exercise the full dispute journey without any manual SQL.
- **Dependencies:** **TDP-DATA-01** (entities + `DbContext`). Enables **TDP-AUTH-01** (login needs seeded users with bcrypt hashes) and all Day 2 API tickets (which query seeded transactions/users). Maps to **Milestone Day 1 — Foundation** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Generate the initial migration

With the design-time factory (below) in place, generate the migration into `src/DisputePortal.Api/Migrations`:

```bash
dotnet ef migrations add InitialCreate \
  --project src/DisputePortal.Api \
  --startup-project src/DisputePortal.Api \
  --output-dir Migrations
```

This produces `Migrations/<timestamp>_InitialCreate.cs` creating the five tables (`users`, `transactions`, `disputes`, `dispute_events`, `resolutions`), unique indexes (`email`, both `reference` columns, `disputes.transaction_id`, `resolutions.dispute_id`), and the FK constraints from TDP-DATA-01. Enum columns are `varchar`/`text` storing string names; `amount` is `numeric(18,2)`; `extracted_fields_json` is `jsonb`; timestamps are `timestamptz`.

### 2.2 Design-time factory — `src/DisputePortal.Api/Data/DesignTimeDbContextFactory.cs`

`dotnet ef` needs to construct the context at design time without full app startup. Provide a factory that reads the connection string (env or a local default):

```csharp
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
```

### 2.3 Auto-migrate on startup — `Program.cs`

After `builder.Build()`, run migrations and seeding inside a scope, before `app.Run()`. Retry to tolerate Postgres still warming up (compose health check makes this rare, but startup ordering plus a short retry is belt-and-braces against the SPEC §4.3 migration risk).

```csharp
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<DisputePortalDbContext>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    // MigrateAsync with a small retry loop (Postgres readiness)
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
```

### 2.4 Idempotent seeder — `src/DisputePortal.Api/Data/DatabaseSeeder.cs`

Seeding must be safe to run on every startup. Guard on `Users.AnyAsync()` — if data exists, skip. Use deterministic GUIDs so references between seed transactions and the customer are stable across runs and across `down -v` cycles.

```csharp
using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Data;

public static class DatabaseSeeder
{
    // Deterministic IDs for stable cross-references and test assertions.
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnalystId  = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagerId  = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(DisputePortalDbContext db, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            logger.LogInformation("Seed skipped: users already present.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // bcrypt work factor 12 (SPEC 3.6 security: >= 12)
        string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw, workFactor: 12);

        var customer = new User
        {
            Id = CustomerId, Email = "maya@example.com",
            PasswordHash = Hash("Password123!"), FullName = "Maya Naidoo",
            Role = UserRole.CUSTOMER, CreatedAt = now
        };
        var analyst = new User
        {
            Id = AnalystId, Email = "sipho@capitec.ops",
            PasswordHash = Hash("Password123!"), FullName = "Sipho Dlamini",
            Role = UserRole.OPS_ANALYST, CreatedAt = now
        };
        var manager = new User
        {
            Id = ManagerId, Email = "zanele@capitec.ops",
            PasswordHash = Hash("Password123!"), FullName = "Zanele Khumalo",
            Role = UserRole.OPS_MANAGER, CreatedAt = now
        };

        await db.Users.AddRangeAsync(customer, analyst, manager);

        var txns = new[]
        {
            NewTxn("TXN-20260710-00001", "Shoprite",      "Grocery Stores",   450.00m, TransactionStatus.SETTLED,  now.AddDays(-11)),
            NewTxn("TXN-20260714-00002", "Shoprite",      "Grocery Stores",   450.00m, TransactionStatus.SETTLED,  now.AddDays(-7)),  // duplicate demo
            NewTxn("TXN-20260715-00003", "Uber",          "Transportation",   129.50m, TransactionStatus.SETTLED,  now.AddDays(-6)),
            NewTxn("TXN-20260716-00004", "Takealot",      "E-commerce",      1299.00m, TransactionStatus.PENDING,  now.AddDays(-5)),
            NewTxn("TXN-20260717-00005", "Unknown Merch", "Uncategorised",   7999.99m, TransactionStatus.SETTLED,  now.AddDays(-4)),  // unauthorised demo
            NewTxn("TXN-20260718-00006", "Netflix",       "Streaming",        199.00m, TransactionStatus.SETTLED,  now.AddDays(-3)),
        };
        await db.Transactions.AddRangeAsync(txns);

        await db.SaveChangesAsync();
        logger.LogInformation("Seed complete: {Users} users, {Txns} transactions.", 3, txns.Length);

        Transaction NewTxn(string reference, string merchant, string mcc, decimal amount,
                           TransactionStatus status, DateTimeOffset date) => new()
        {
            Id = Guid.NewGuid(), CustomerId = CustomerId, Reference = reference,
            MerchantName = merchant, MerchantCategory = mcc, Amount = amount,
            Currency = "ZAR", TransactionDate = date, Status = status, CreatedAt = now
        };
    }
}
```

### 2.5 Seed accounts (documented in README / TDP-DOC-02)

| Role | Email | Password | Persona |
|---|---|---|---|
| `CUSTOMER` | `maya@example.com` | `Password123!` | Maya (SPEC §1.3) |
| `OPS_ANALYST` | `sipho@capitec.ops` | `Password123!` | Sipho (SPEC §1.3) |
| `OPS_MANAGER` | `zanele@capitec.ops` | `Password123!` | Zanele (SPEC §1.3) |

> These are demo credentials for a local, non-production, single-command submission. The README (TDP-DOC-02) must state this explicitly. Passwords are only ever stored as bcrypt hashes; the plaintext lives solely in the seeder and docs.

### 2.6 Transaction data rationale

The seed set is crafted to demo the SPEC journeys: two identical `R450 Shoprite` charges (Journey 1 duplicate-charge narrative), a high-value `R7999.99 Unknown Merch` (unauthorised / HIGH-priority classification demo per AI-02 rules in SPEC §3.5), plus everyday spend. All belong to the seeded customer so the transaction list (TXN-01) is populated on first login.

## 3. Acceptance Criteria

- On first API startup against an empty database, `MigrateAsync()` creates all five tables with the schema from TDP-DATA-01 (verified via `\dt` / information_schema).
- After startup, the DB contains exactly three users (one per role) and six transactions, all owned by the seeded customer.
- Seeding is idempotent: restarting the API (without `down -v`) does not duplicate users or transactions; the log shows "Seed skipped".
- `docker compose down -v && docker compose up --build` reproduces an identical seeded state (deterministic user IDs, same references) — satisfies SPEC §4.4 fresh-state check.
- All seeded passwords are stored as bcrypt hashes with work factor 12; no plaintext password is persisted (SPEC §3.6 Security).
- The seeded customer can immediately authenticate via `/api/v1/auth/login` (once TDP-AUTH-01 lands) and retrieve their transactions.
- Migration files are committed under `src/DisputePortal.Api/Migrations` and included in the Docker image (SPEC §4.3 "include migration scripts in repo").
- Startup tolerates a briefly-unavailable Postgres via the retry loop without crashing the container.

## 4. Technical Notes

- **`MigrateAsync()` vs `EnsureCreated()`:** use `MigrateAsync()` (SPEC §3.6, §4.3). `EnsureCreated()` bypasses the migrations history and must never be used here — it would break future migrations and diverge from the committed schema.
- **bcrypt work factor:** `BCrypt.Net.BCrypt.HashPassword(pw, workFactor: 12)`. Factor 12 meets the SPEC §3.6 "≥ 12" bar; higher factors slow seeding noticeably (each hash ~250ms at 12), so 12 is the sweet spot for the demo.
- **Idempotency guard:** `Users.AnyAsync()` is sufficient because the named volume `pgdata` (TDP-INFRA-02) persists data between `up`/`down` (without `-v`). Only `down -v` wipes it, triggering a fresh reseed.
- **Deterministic GUIDs** for users make integration tests (TDP-TEST-01) and the auth ticket able to assert against known IDs; transactions use `Guid.NewGuid()` since only the customer linkage matters.
- **Do not seed disputes/resolutions:** those are created through the live flow so the demo exercises real code paths (submit → classify → resolve). Seeding them would mask bugs.
- **Concurrency on startup:** with a single API instance this is fine. If the Kafka consumer group ever scales the API to multiple instances (SPEC §3.6 Scalability), migration/seed should run once — acceptable for this submission's single-instance compose, but note it as a future hardening (advisory lock).
- **`ConnectionStrings__Default`** is injected by compose (SPEC §3.1); the design-time factory falls back to `localhost` for local `dotnet ef` runs.

## 5. Definition of Done

- [ ] `DesignTimeDbContextFactory` implemented so `dotnet ef` commands work.
- [ ] `InitialCreate` migration generated and committed under `Migrations/`.
- [ ] `MigrateAsync()` runs on startup with a bounded retry loop, before `app.Run()`.
- [ ] `DatabaseSeeder.SeedAsync` implemented: idempotent, three role users with bcrypt(12) hashes, six demo transactions.
- [ ] Seed credentials table documented for handoff to README (TDP-DOC-02).
- [ ] Verified: fresh `docker compose up --build` creates schema + seed; second start skips seeding; `down -v` + up reproduces identical state.
- [ ] No plaintext passwords persisted; hashes confirmed bcrypt work factor 12.
- [ ] Reviewed and merged; unblocks TDP-AUTH-01 and Day 2 API tickets.
