# Transactions Dispute Portal

![CI](https://github.com/DevBongz/Transactions-Dispute-Portal/actions/workflows/ci.yml/badge.svg?branch=main)

A full-stack **.NET 8 + React** application that lets bank customers view card/journal transactions,
raise disputes (structured form or natural language), track them through a timeline, and receive
plain-language resolution summaries. Operations analysts triage and resolve cases with AI assistance.

Built in the context of Capitec Bank's **DMC (Digital Merchant Commerce)** financial motion
(Option 2 — Fin-Motion). See [`SPEC.md`](SPEC.md) for the authoritative product specification.

## Architecture

```
Browser (React SPA) → .NET 8 Web API → PostgreSQL 16
                    ↘ Kafka (dispute.submitted / classified / resolved)
Background consumer classifies new disputes via Google Gemini and publishes dispute.classified.
Resolution publishes dispute.resolved. Notifications are simulated (stored events / UI), not emailed.
```

| Layer | Technology |
|-------|------------|
| Frontend | React 18 + TypeScript, Vite, Tailwind, Radix/shadcn-style UI, TanStack Query, nginx |
| Backend | ASP.NET Core 8, Serilog, Swagger/OpenAPI, JWT auth |
| Data | PostgreSQL 16, EF Core (Npgsql), auto-migrate + seed on startup |
| Messaging | Apache Kafka (Compose) / Redpanda (Render) — topics above |
| AI | Google Gemini (`gemini-2.5-flash` + free-tier fallbacks) — extract, classify, summarise |
| Auth | Self-contained JWT (60 min), bcrypt (≥ 12), roles `CUSTOMER` / `OPS_ANALYST` / `OPS_MANAGER` |

## Prerequisites

**Required for Quick Start**

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or compatible Docker engine)
- A [Google AI Studio](https://aistudio.google.com/apikey) API key (`GEMINI_API_KEY`)

**Optional (local non-Docker development)**

- .NET 8 SDK
- Node.js 20+ (22 also supported in CI)

## Quick Start

> First `docker compose up --build` is slower (image builds + Kafka warm-up). Warm restarts are much faster; the §1.1 “under 2 minutes” target assumes a warm machine.

1. Clone and enter the repo:

   ```bash
   git clone https://github.com/DevBongz/Transactions-Dispute-Portal.git
   cd Transactions-Dispute-Portal
   ```

2. Create a `.env` in the repo root (Compose reads it automatically):

   ```bash
   cp .env.example .env
   ```

   Edit `.env`:

   ```dotenv
   GEMINI_API_KEY=AIza...your-google-ai-studio-key...
   JWT_SECRET=change-me-to-a-long-random-string-at-least-32-chars
   ```

3. Bring the stack up:

   ```bash
   docker compose up --build
   ```

4. Open:

   | Surface | URL |
   |---------|-----|
   | Frontend | http://localhost:3000 |
   | API | http://localhost:5000 |
   | Swagger UI | http://localhost:5000/swagger |

   The API runs EF Core migrations and seeds demo data on startup — no manual DB setup.

5. Log in with a [seed account](#seed-accounts) and follow [Using the App](#using-the-app).

Reset to a clean database:

```bash
docker compose down -v && docker compose up --build
```

## Environment Variables

| Variable | Required | Default (compose) | Purpose |
|----------|----------|-------------------|---------|
| `GEMINI_API_KEY` | **Yes** (for AI features) | none | Server-side Google Gemini key for extraction, classification, and resolution summaries. **Never** exposed to the frontend. |
| `JWT_SECRET` | **Yes** | insecure dev default | HMAC signing key for JWTs (60-min expiry). **Must be overridden** with ≥ 32 characters. |

Also set in `docker-compose.yml` (no action needed): `ConnectionStrings__Default`, `Kafka__BootstrapServers`, `Cors__AllowedOrigins__0`.

> **JWT_SECRET warning (SPEC §4.3):** a weak or default secret lets tokens be forged. Always set a strong random `JWT_SECRET` (≥ 32 chars). The compose default exists only so the stack boots for local evaluation.

> If `GEMINI_API_KEY` is missing or invalid, AI features degrade gracefully — classification falls back to `CLASSIFICATION_FAILED` / “Needs Triage”, and disputes can still be submitted and resolved manually (SPEC §3.6 Reliability).

## Seed Accounts

Password for all demo users: `Password123!` (bcrypt-hashed in the DB; plaintext only for local demo).

| Role | Email | What you can do |
|------|-------|-----------------|
| Customer | `maya@example.com` | View transactions, raise & track disputes |
| Ops analyst | `sipho@capitec.ops` | Triage queue, resolve disputes, generate summaries |
| Ops manager | `zanele@capitec.ops` | Dashboard metrics (open counts, avg resolution time) |

Maya is pre-seeded with sample transactions so you can raise a dispute immediately.

## Using the App

### Customer — raise a dispute in your own words (SPEC Journey 1)

1. Log in as **maya@example.com**.
2. Open **Transactions**, pick a row → **Dispute this transaction**.
3. Choose **Describe in your own words** and type e.g.  
   *“I was charged R450 twice at Shoprite on 14 July but I only shopped once.”*
4. Click **Extract details** — AI pre-fills category / amount / merchant / date for review.
5. Confirm or edit, then **Submit dispute**. Note the reference (`DSP-YYYYMMDD-NNNNN`).
6. **My Disputes** shows status and timeline; after ops resolves, the plain-language summary appears.

### Ops — resolve a dispute (SPEC Journey 2)

1. Log in as **sipho@capitec.ops**.
2. Open the ops queue (priority order). Cases with failed AI triage show **Needs Triage**.
3. Open a dispute, review customer + transaction + description.
4. **Resolve** → outcome (**Upheld** / **Declined** / **Partial**) + internal notes (≥ 20 chars).
5. **Generate summary** → review/edit → **Confirm resolution**.
6. Customer sees status **Resolved** and the summary.

**Outcomes:** **Upheld** = customer claim accepted; **Declined** = not accepted; **Partial** = partly accepted.

## API & Swagger

- Base path: `http://localhost:5000/api/v1`
- Interactive docs: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- Authorize with the JWT from `POST /api/v1/auth/login` (paste token only — no `Bearer ` prefix in the Swagger dialog).
- Groups: Auth, Transactions, Disputes, AI, Dashboard (ops).

## Local Development (without full Compose for the UI/API)

With Postgres + Kafka still via Compose (or local equivalents):

```bash
# API
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/DisputePortal.Api
# listens on http://localhost:5000 when configured; see launchSettings / ASPNETCORE_URLS

# UI
cd src/dispute-portal-ui
npm ci
npm run dev   # http://localhost:3000 — proxies /api → :5000
```

## Testing

### Backend (xUnit + Testcontainers)

Requires a running Docker daemon for integration tests.

```bash
dotnet test DisputePortal.sln                     # unit + integration
dotnet test src/DisputePortal.Api.Tests           # unit / JWT pipeline only (no Docker)
dotnet test tests/DisputePortal.IntegrationTests  # WebApplicationFactory + Postgres 16
```

Integration tests spin up ephemeral PostgreSQL via Testcontainers, fake Kafka + Gemini, and exercise
`POST /disputes`, resolve, and `POST /ai/extract-dispute`.

### Frontend (Vitest + RTL + MSW)

```bash
cd src/dispute-portal-ui
npm ci
npm run test        # vitest run
npm run coverage    # optional coverage report
```

## Manual QA Checklist (SPEC §4.4)

- [ ] `docker compose up --build` completes without errors on a clean machine
- [ ] Seed customer and ops accounts work
- [ ] Full customer journey: login → transactions → NL dispute → see reference
- [ ] Full ops journey: login → queue → resolve → auto-generated summary
- [ ] Swagger UI lists endpoints and shows schemas; Authorize works
- [ ] `docker compose down -v && docker compose up --build` works identically

## Project Layout

```
DisputePortal.sln
src/
  DisputePortal.Api/              # ASP.NET Core 8 Web API
  DisputePortal.Api.Tests/        # Backend unit tests
  dispute-portal-ui/              # React + TypeScript SPA
tests/
  DisputePortal.IntegrationTests/ # Testcontainers + WebApplicationFactory
docs/tickets/                     # Jira-style development tickets
docker-compose.yml
render.yaml                       # Optional Render Blueprint (cloud demo)
DEPLOY-RENDER.md
DEMO-SCRIPT.md
SPEC.md
```

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on every push/PR to `main`:

- Backend: `dotnet restore` → `build` → `test` (including Testcontainers)
- Frontend: Node 20 & 22 matrix — `npm ci` → `build` → `test`

No repository secrets are required for CI (throwaway JWT / Gemini values are inlined).

## Troubleshooting

- **Ports in use (3000 / 5000 / 5432 / 9092):** stop the conflicting process or change mappings in `docker-compose.yml`.
- **AI extract 502 / classification “Needs Triage”:** check `GEMINI_API_KEY` in `.env` (or Render `Gemini__ApiKey`). The rest of the app still works.
- **401 on every API call:** JWT expired (60 min) — log in again.
- **Kafka not ready on first boot:** the API retries topic creation; wait 1–2 minutes on first `--build`.
- **Compose Kafka dual listeners:** containers use `kafka:29092`; host tools use `localhost:9092` (documented deviation from a single SPEC listener — required for both in-container and host access).
- **Stale data:** `docker compose down -v` then re-up.

## Notes & Out of Scope (SPEC §1.2)

- Notifications are **simulated** (timeline / UI), not real email/SMS.
- Auth is **embedded JWT**, not Keycloak / external IdP.
- No live payment-network chargeback integration — transactions are seeded.
- Cloud hosting (e.g. Render) is optional for demos; the graded path is local Compose.

## Related docs

- [`SPEC.md`](SPEC.md) — product & technical specification  
- [`docs/tickets/`](docs/tickets/) — ticket index and DoD  
- [`DEMO-SCRIPT.md`](DEMO-SCRIPT.md) — interview walkthrough  
- [`DEPLOY-RENDER.md`](DEPLOY-RENDER.md) / [`DEPLOYMENT-ISSUES-AND-FIXES.md`](DEPLOYMENT-ISSUES-AND-FIXES.md) — cloud deploy notes  
