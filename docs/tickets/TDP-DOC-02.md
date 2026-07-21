# TDP-DOC-02 — README & Operational Runbook

**Jira summary:** Author the top-level `README.md` that lets a reviewer clone the Transactions Dispute Portal and bring the full stack online with a single command. It must document prerequisites, the `docker compose up --build` workflow, required environment variables (`ANTHROPIC_API_KEY`, `JWT_SECRET`), seeded demo accounts, build/run/test instructions for both backend and frontend, an architecture overview, the API/Swagger surface, and the manual QA checklist from §4.4. This is the front door of the submission and directly supports the §1.1 objective "runnable from a single command" and AC-NFR.

## 1. Context & Motivation

- **Background:** The project is a public GitHub submission (§4.2) evaluated by a reviewer who has not seen the code. The spec lists "README with full build/run/test instructions" as in-scope (§1.2) and calls out a JWT-secret misconfiguration risk mitigated by "documented in README with mandatory override warning" (§4.3). No README exists yet beyond the repo scaffold.
- **Business Impact:** A reviewer's first five minutes decide the perceived quality of the whole submission. If `docker compose up --build` does not "just work" with clear env-var guidance and demo logins, the graded objective (§1.1: full stack online in under 2 minutes) and the QA journeys (§4.4) cannot be verified. This ticket removes that friction.
- **User Story:** As a reviewer, I want a single README that tells me exactly how to configure, run, test, and exercise the app so that I can evaluate it end-to-end without reading source or asking questions.
- **Dependencies:** TDP-INFRA-02 (compose stack) and effectively all feature tickets (their behaviour is documented here); references TDP-DOC-01 (Swagger), TDP-TEST-01/02 (test commands), TDP-DATA-02 (seed accounts). Milestone: **Day 7 (22 Jul)**.

## 2. Detailed Description

### 2.1 README structure (top-level `README.md`)

```
# Transactions Dispute Portal
1.  Overview            — one-paragraph what/why + DMC Fin-Motion context, key features
2.  Architecture        — diagram + component/tech table
3.  Prerequisites       — Docker Desktop, an Anthropic API key; (optional) .NET 8 SDK, Node 20+
4.  Quick Start         — .env setup → docker compose up --build → URLs
5.  Environment Variables — table: name, required?, default, purpose
6.  Seed Accounts       — demo customer + ops logins (email / password / role)
7.  Using the App       — customer journey + ops journey walkthroughs
8.  API & Swagger       — base URL, /swagger link, endpoint summary
9.  Local Development    — run backend & frontend outside Docker
10. Testing             — backend (dotnet test) + frontend (npm run test)
11. Manual QA Checklist  — the §4.4 pre-submission checklist
12. Project Layout      — directory tree
13. Troubleshooting     — common issues (ports, Kafka, missing key)
14. Notes & Out-of-Scope — simulated notifications, embedded JWT, etc.
```

### 2.2 Quick Start (the critical section)

```markdown
## Quick Start

> Requires **Docker Desktop** and an **Anthropic API key**. Nothing else needs to be installed.

1. Clone and enter the repo:
   ```bash
   git clone https://github.com/<owner>/dmc-fin-motion_topicproducer.git
   cd dmc-fin-motion_topicproducer
   ```
2. Create a `.env` file in the repo root (compose reads it automatically):
   ```dotenv
   ANTHROPIC_API_KEY=sk-ant-...your-key...
   JWT_SECRET=change-me-to-a-long-random-string-at-least-32-chars
   ```
3. Bring the whole stack up:
   ```bash
   docker compose up --build
   ```
4. Open the app:
   | Surface     | URL                              |
   |-------------|----------------------------------|
   | Frontend    | http://localhost:3000            |
   | API         | http://localhost:5000            |
   | Swagger UI  | http://localhost:5000/swagger    |

   The API runs EF Core migrations and seeds demo data automatically on startup — no manual DB setup.

5. Log in with a seeded account (below) and follow the walkthroughs in **Using the App**.

To reset to a clean state:
```bash
docker compose down -v && docker compose up --build
```
```

### 2.3 Environment variables

```markdown
## Environment Variables

| Variable            | Required | Default (compose)        | Purpose |
|---------------------|----------|--------------------------|---------|
| `ANTHROPIC_API_KEY` | **Yes**  | none                     | Server-side Anthropic Claude key for extraction, classification, and resolution summaries. Never exposed to the frontend. |
| `JWT_SECRET`        | **Yes**  | insecure dev default     | HMAC signing key for JWTs (60-min expiry). **Must be overridden** with a long random value — see warning below. |

The API also reads (pre-set in `docker-compose.yml`, no action needed):
`ConnectionStrings__Default`, `Kafka__BootstrapServers`.

> ⚠️ **JWT_SECRET warning (per SPEC §4.3):** a weak or default secret lets tokens be forged.
> Always set a strong random `JWT_SECRET` (≥ 32 chars). The compose default exists only so the
> stack boots for a quick demo and must not be used beyond local evaluation.

> 🔒 The Anthropic key is read only by the backend (`Anthropic__ApiKey`). If it is missing or invalid,
> AI features degrade gracefully — classification falls back to `CLASSIFICATION_FAILED` and disputes
> are still submitted and resolvable (SPEC §3.6 Reliability).
```

### 2.4 Seed accounts

Sourced from TDP-DATA-02 seed data; passwords are bcrypt-hashed (work factor ≥ 12, §3.6). Document the plaintext demo credentials the seeder uses:

```markdown
## Seed Accounts

| Role         | Email                       | Password       | What you can do |
|--------------|-----------------------------|----------------|-----------------|
| Customer     | maya@example.com            | Password123!   | View transactions, raise & track disputes |
| Ops Analyst  | sipho@capitec.example       | Password123!   | Triage queue, resolve disputes, generate summaries |
| Ops Manager  | zanele@capitec.example      | Password123!   | Dashboard metrics (open counts, avg resolution time) |

Maya's account is pre-seeded with sample transactions so you can raise a dispute immediately.
```

> Confirm exact emails/passwords against the TDP-DATA-02 seeder before publishing; the table must match the code.

### 2.5 Using the app — journey walkthroughs

Condense SPEC §2.2 into two reviewer-runnable scripts:

```markdown
## Using the App

### Customer — raise a dispute in your own words (SPEC Journey 1)
1. Log in as **maya@example.com**.
2. On **Transactions**, find a transaction and click **Dispute this transaction**.
3. Choose the **Describe in your own words** tab and type e.g.
   *"I was charged R450 twice at Shoprite on 14 July but I only shopped once."*
4. Click **Extract** — the AI pre-fills reason, amount, merchant, date for review.
5. Adjust if needed and **Submit**. Note the reference, e.g. `DSP-20260714-00042`.
6. Go to **My Disputes** to see status and, once resolved, the plain-language summary.

### Ops — resolve a dispute (SPEC Journey 2)
1. Log in as **sipho@capitec.example**.
2. The **Operations Dashboard** lists open disputes by priority (Critical first).
3. Open a dispute, review customer info, transaction, and AI category/priority.
4. Click **Resolve**, pick an outcome (Upheld / Declined / Partial), add internal notes (≥ 20 chars).
5. Click **Generate Summary** to draft the customer-facing message, review/edit, then **Confirm Resolution**.
```

### 2.6 Testing section

```markdown
## Testing

### Backend (xUnit + Testcontainers)  — requires a running Docker daemon
```bash
dotnet test DisputePortal.sln                     # unit + integration
dotnet test tests/DisputePortal.UnitTests         # fast unit-only, no Docker
```
Integration tests spin up an ephemeral PostgreSQL 16 container via Testcontainers and boot the API
through `WebApplicationFactory<Program>` (Kafka + Anthropic are faked). See TDP-TEST-01.

### Frontend (Vitest + React Testing Library)
```bash
cd src/dispute-portal-ui
npm ci
npm run test        # vitest run
npm run coverage    # coverage report
```
See TDP-TEST-02.
```

### 2.7 Architecture overview

Include a trimmed version of the §3.1 diagram plus a component table:

```markdown
## Architecture

Browser (React SPA) → .NET 8 Web API (Controllers → Services → Repositories → EF Core)
→ PostgreSQL 16, and Apache Kafka. A hosted background consumer reads `dispute.submitted`,
calls Anthropic Claude to classify, and publishes `dispute.classified`. Resolution publishes
`dispute.resolved`.

| Layer      | Technology                                   |
|------------|----------------------------------------------|
| Frontend   | React + TypeScript, shadcn/ui, TanStack Query (nginx) |
| Backend    | ASP.NET Core (.NET 8), Serilog, Swagger      |
| Data       | PostgreSQL 16, EF Core (Npgsql), auto-migrate on startup |
| Messaging  | Apache Kafka — topics `dispute.submitted`, `dispute.classified`, `dispute.resolved` |
| AI         | Anthropic Claude — `claude-haiku-4-5-20251001` (extract/classify), `claude-sonnet-5` (summaries) |
| Auth       | Self-contained JWT (60 min), bcrypt (≥ 12), roles CUSTOMER / OPS_ANALYST / OPS_MANAGER |
```

### 2.8 Manual QA checklist (verbatim from SPEC §4.4)

```markdown
## Manual QA Checklist (pre-submission)

- [ ] `docker compose up --build` completes without errors on a clean machine
- [ ] Seed customer and ops accounts work
- [ ] Full customer journey: login → view transactions → submit NL dispute → view dispute reference
- [ ] Full ops journey: login → open ops queue → resolve dispute → view auto-generated summary
- [ ] Swagger UI lists all endpoints and shows correct schemas
- [ ] `docker compose down -v && docker compose up --build` (fresh state) works identically
```

### 2.9 Troubleshooting

```markdown
## Troubleshooting

- **Ports already in use (3000/5000/5432/9092):** stop the conflicting process or edit the port
  mappings in `docker-compose.yml`.
- **AI features do nothing / classification shows `CLASSIFICATION_FAILED`:** check `ANTHROPIC_API_KEY`
  is set in `.env`; the rest of the app still works without it.
- **401 on every API call:** your JWT expired (60-min lifetime) — log in again.
- **Kafka not ready on first boot:** the API retries; give the stack ~1–2 minutes on first `--build`.
- **Stale data / migration weirdness:** `docker compose down -v` to drop volumes, then re-up.
```

## 3. Acceptance Criteria

- `README.md` exists at the repository root and covers every section in §2.1.
- Quick Start documents `.env` creation with `ANTHROPIC_API_KEY` and `JWT_SECRET`, then `docker compose up --build`, and lists the three URLs — frontend `:3000`, API `:5000`, Swagger `:5000/swagger` (AC-NFR).
- The env-var table marks both variables required and includes the mandatory `JWT_SECRET` override warning (§4.3) and the note that the Anthropic key is server-side only (§3.6).
- Seed-account table lists a working customer and ops login matching the TDP-DATA-02 seeder.
- Backend (`dotnet test`) and frontend (`npm run test`) instructions are present and correct, noting the Testcontainers Docker requirement.
- The architecture overview names all layers, the three Kafka topics, and both Claude models.
- The manual QA checklist from §4.4 is reproduced.
- A reviewer following only the README can bring the stack up and complete both journeys without consulting source code.

## 4. Technical Notes

- **`.env` is git-ignored:** ensure `.gitignore` excludes `.env`; provide a committed `.env.example` with placeholder values so reviewers know the shape. Never commit a real Anthropic key.
- **Keep credentials in sync:** the seed-account table must exactly match TDP-DATA-02. If the seeder changes, update the README in the same PR.
- **Under-2-minutes claim (§1.1):** note that the first `--build` is slower (image builds + Kafka warm-up); the 2-minute target is for a warm build. Set reviewer expectations accordingly.
- **Relative Swagger link:** always `http://localhost:5000/swagger` externally (host port), not the container's `8080` — mirrors TDP-DOC-01.
- **Cross-link, don't duplicate:** reference TDP-DOC-01 for the API contract and the ticket docs under `docs/tickets/` rather than restating every endpoint; keep the README focused on running and evaluating.
- **Out-of-scope callouts (§1.2):** state that notifications are simulated (stored in DB / shown in UI), auth is embedded (no external Keycloak), and there is no real payment-network integration, so reviewers don't expect them.

## 5. Definition of Done

- [ ] Root `README.md` written with all §2.1 sections.
- [ ] `.env.example` committed; `.env` git-ignored; no secrets in version control.
- [ ] Quick Start verified end-to-end on a clean checkout: `.env` → `docker compose up --build` → all three URLs reachable.
- [ ] Seed-account table verified against the running app (both logins work).
- [ ] Backend and frontend test commands verified to run as documented.
- [ ] Manual QA checklist (§4.4) reproduced and all items pass on a fresh `down -v && up --build`.
- [ ] Architecture overview and troubleshooting sections reviewed for accuracy.
- [ ] Reviewed and merged to `main`; public GitHub repo renders the README correctly.
