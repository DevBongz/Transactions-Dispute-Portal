# Build & Migration Notes — DisputePortal.Api

This project was authored without a .NET 8 SDK / Docker on the build machine, so
the EF Core migration has **not** been generated yet. Everything else (entities,
`DisputePortalDbContext`, entity configurations, `DesignTimeDbContextFactory`,
`DatabaseSeeder`, and the `MigrateAsync()` startup wiring) is in place and ready.

The `Migrations/` folder currently holds only a `.gitkeep`. Generate the initial
migration once the toolchain is installed — do **not** hand-write the snapshot.

## 1. Restore & build (from repo root)

```bash
dotnet restore DisputePortal.sln
dotnet build DisputePortal.sln
```

## 2. Generate the initial EF Core migration

Install the EF tool if needed, then generate `InitialCreate` into `Migrations/`:

```bash
dotnet tool install --global dotnet-ef        # first time only
dotnet ef migrations add InitialCreate \
  --project src/DisputePortal.Api \
  --startup-project src/DisputePortal.Api \
  --output-dir Migrations
```

This produces `Migrations/<timestamp>_InitialCreate.cs` (+ the model snapshot)
creating the five tables (`users`, `transactions`, `disputes`, `dispute_events`,
`resolutions`), the unique indexes (`email`, both `reference` columns,
`disputes.transaction_id`, `resolutions.dispute_id`), and the FK constraints.
Enum columns are stored as strings; `amount` is `numeric(18,2)`;
`extracted_fields_json` is `jsonb`; timestamps are `timestamptz`.

The design-time factory falls back to
`Host=localhost;Database=disputeportal;Username=dp_user;Password=dp_pass`, so a
local Postgres is only needed if you want to apply the migration by hand; the API
runs `MigrateAsync()` itself on startup.

Commit the generated files under `src/DisputePortal.Api/Migrations/`.

## 3. Bring the stack up and verify

```bash
cp .env.example .env      # then edit: set a real GEMINI_API_KEY and a >=32-char JWT_SECRET
docker compose up --build
```

Verify:

- Frontend: http://localhost:3000  (requires TDP-FE-01 real build; see note below)
- API:      http://localhost:5000
- Swagger:  http://localhost:5000/swagger
- Health:   http://localhost:5000/health/ready  → `{ "status": "ready" }`

Confirm migration + seed ran (logs should show "Seed complete: 3 users, 6
transactions."). Then:

```bash
docker compose down -v && docker compose up --build   # fresh state reproduces identical seed
```

> **Frontend note:** `src/dispute-portal-ui` is a placeholder (TDP-INFRA-01). Its
> Docker build needs the real Vite app from TDP-FE-01. Until that lands, either
> comment out the `frontend` service or expect only its build to be deferred —
> the api/postgres/kafka/zookeeper bring-up is independent.
