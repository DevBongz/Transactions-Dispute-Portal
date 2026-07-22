# Transactions Dispute Portal

A full-stack .NET 8 + React application that lets customers view transactions,
raise disputes, and track them to resolution, with AI-assisted classification
and resolution summaries. Built in the context of Capitec Bank's DMC (Digital
Merchant Commerce) financial motion.

> **Stub README.** Full build/run/test documentation is authored in TDP-DOC-02.
> See `SPEC.md` for the authoritative specification and `docs/tickets/` for the
> implementation tickets.

## Quick start (once tooling is installed)

```bash
cp .env.example .env    # fill in ANTHROPIC_API_KEY and JWT_SECRET
docker compose up --build
```

- Frontend: http://localhost:3000
- API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger

## Repository layout

```
DisputePortal.sln
src/
  DisputePortal.Api/         # ASP.NET Core 8 Web API
  DisputePortal.Api.Tests/   # xUnit test project
  dispute-portal-ui/         # React + TypeScript SPA (scaffolded in TDP-FE-01)
docker-compose.yml
```

## Seed accounts (local demo only)

| Role          | Email                 | Password       |
|---------------|-----------------------|----------------|
| `CUSTOMER`    | `maya@example.com`    | `Password123!` |
| `OPS_ANALYST` | `sipho@capitec.ops`   | `Password123!` |
| `OPS_MANAGER` | `zanele@capitec.ops`  | `Password123!` |

These are demo credentials for a local, non-production, single-command
submission. Passwords are only ever stored as bcrypt hashes.
