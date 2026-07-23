# Render Deployment Postmortem & Interview Notes

**Project:** Transactions Dispute Portal  
**Deployment target:** Render (Option B — full fidelity)  
**Stack:** .NET 8 API · React SPA · Managed Postgres · Redpanda (Kafka) · Google Gemini  
**Live URLs (this workspace):**
- UI: https://dp-ui-5rd4.onrender.com  
- API: https://dp-api-eebu.onrender.com  
- Health: https://dp-api-eebu.onrender.com/health/ready  
- Swagger: https://dp-api-eebu.onrender.com/swagger  

**Demo logins** (password for all: `Password123!`):

| Role | Email |
|---|---|
| Customer | `maya@example.com` |
| Ops analyst | `sipho@capitec.ops` |
| Ops manager | `zanele@capitec.ops` |

This document captures the issues encountered while deploying to production for demo, the root causes, and the fixes applied. Use it for demo prep and interview talking points.

---

## 1. Deployment approach chosen

### Options considered
- **Option A — Single VM + Docker Compose:** one box, full local parity, more ops burden (HTTPS, DNS, process supervision).
- **Option B — Render Blueprint (chosen):** managed Postgres, Redpanda as a private service, Docker API, static SPA — closer to “real” cloud wiring, shareable HTTPS URLs.

### Why Option B
- Shareable HTTPS demo without managing a VM.
- Blueprint (`render.yaml`) encodes infrastructure as code.
- Private network between API ↔ Postgres ↔ Redpanda.
- Trade-off accepted: higher monthly cost (~Starter instances) and platform-specific quirks (documented below).

### Key artefacts added
| File | Purpose |
|---|---|
| `render.yaml` | Blueprint: Postgres, Redpanda, API, SPA |
| `DEPLOY-RENDER.md` | Step-by-step runbook |
| `NpgsqlConnectionString.Normalize` | Accept managed `postgres://` URLs |
| `GeminiClient` / `GeminiOptions` | Free-tier LLM instead of Anthropic |

---

## 2. Issues, root causes, and solutions

### Issue 1 — Blueprint could not fetch the Redpanda image

**Symptom**
```text
services[0].image
the provided URL (docker.redpanda.com/redpandadata/redpanda:v24.2.7) could not be fetched.
```

**Root cause**  
Render Blueprint validation cannot pull from Redpanda’s private registry (`docker.redpanda.com`).

**Solution**  
Switch the image to Docker Hub:

```yaml
image:
  url: docker.io/redpandadata/redpanda:v24.2.7
```

**Interview angle:** “We validated Blueprint constraints early; third-party registries aren’t always reachable from the platform’s validation path, so we pinned a public mirror.”

---

### Issue 2 — Anthropic billing blocked AI features

**Symptom**  
Could not add credit to an Anthropic account; Blueprint prompted for `Anthropic__ApiKey`.

**Root cause**  
SPEC originally targeted Anthropic Claude. Demo blocked on paid credit.

**Solution**  
Swapped the LLM provider to **Google Gemini** (free API key via [Google AI Studio](https://aistudio.google.com/apikey)):
- Replaced `AnthropicClient` with `GeminiClient` (`generateContent` API).
- Config section `Gemini` / env `Gemini__ApiKey` / `GEMINI_API_KEY`.
- Default model: `gemini-2.0-flash` for extraction, classification, and summaries.
- Kept the existing `IAnthropicClient` completion contract so feature services and tests stayed stable (historical interface name).

**Interview angle:** “Provider choice was abstracted behind a thin completion client. When billing blocked us, we swapped the transport without rewriting dispute/classification/summary flows.”

---

### Issue 3 — Redpanda: `unrecognised option '--mode'`

**Symptom**
```text
ERROR main - cli_parser.cc:46 - Argument parse error: unrecognised option '--mode'
```

**Root cause**  
`--mode` is an **`rpk`** flag, not a flag on the Redpanda C++ binary. Render’s `dockerCommand` replaced the full process command, so `redpanda start --mode …` invoked the binary directly and rejected `--mode`.

**Solution**  
Start via `rpk`:

```text
rpk redpanda start --mode dev-container --smp 1 --memory 400M \
  --kafka-addr PLAINTEXT://0.0.0.0:9092 \
  --advertise-kafka-addr PLAINTEXT://dp-redpanda:9092 \
  --set redpanda.auto_create_topics_enabled=true
```

**Interview angle:** “Container entrypoints matter. Platform `dockerCommand` overrides can bypass the image’s wrapper (`rpk`); we aligned the command with how Redpanda expects to be started in containers.”

---

### Issue 4 — Redpanda: insufficient physical memory on Starter

**Symptom**
```text
Could not initialize seastar: std::runtime_error
(insufficient physical memory: needed 536870912 available 500000000)
```

**Root cause**  
Render **Starter** (~512 MB) leaves ~500 MB usable after OS overhead. Requesting `--memory 512M` exceeded available RAM.

**Solution**  
Lower memory request to `--memory 400M` (or bump instance to Standard).

**Interview angle:** “We sized the broker for the instance class, not for a laptop Docker Desktop default. Small cloud VMs need explicit memory headroom.”

---

### Issue 5 — API health check: Postgres connection string format

**Symptom**
```text
Format of the initialization string does not conform to specification starting at index 0.
/health/ready → 503 (postgres Unhealthy)
```

**Root cause**  
Render injects Postgres as a **URI** (`postgresql://user:pass@host/db`).  
- EF Core used `NpgsqlConnectionString.Normalize` → OK.  
- Health check (`AddNpgSql`) used the **raw** URI → Npgsql rejected it.

**Solution**  
Normalize the connection string for health checks the same way as DbContext:

```csharp
var connectionString = NpgsqlConnectionString.Normalize(
    builder.Configuration.GetConnectionString("Default"))!;
builder.Services.AddHealthChecks().AddNpgSql(connectionString, ...);
```

Normalize converts URI → key-value form and sets `Ssl Mode=Require` for managed Postgres.

**Interview angle:** “Config that works for one library path isn’t automatically shared with another. We treated connection-string normalisation as a shared infrastructure concern.”

---

### Issue 6 — Kafka: `Unknown topic or partition` on consume

**Symptom**
```text
Subscribed topic not available: dispute.submitted: Broker: Unknown topic or partition
```

**Root cause**  
API started while Redpanda was still failing/restarting. Topic initializer gave up after a few attempts. Consumers don’t create topics; producers/admin do.

**Solution**
1. Get Redpanda **Live** first (Issues 3–4).
2. Increase topic-init retries (`KafkaTopicInitializer` → 20 attempts).
3. Redeploy API so topics are ensured: `dispute.submitted`, `dispute.classified`, `dispute.resolved`.

Consumer already retries with backoff — non-fatal until topics exist.

**Interview angle:** “At-least-once consumers + startup ordering. We made topic ensure idempotent and retry longer for cold cloud starts, without blocking HTTP traffic.”

---

### Issue 7 — Render service name suffixes broke URL wiring

**Symptom**  
Expected `dp-api.onrender.com` / `dp-ui.onrender.com`, but workspace got:
- API: `dp-api-eebu.onrender.com`
- UI: `dp-ui-5rd4.onrender.com`

**Root cause**  
Blueprint service names weren’t globally unique in the workspace; Render appended suffixes. Hardcoded cross-links in `render.yaml` then pointed at the wrong hosts.

**Solution**  
Manually align env vars to live hostnames:

| Service | Variable | Value |
|---|---|---|
| `dp-api` | `Cors__AllowedOrigins__0` | `https://dp-ui-5rd4.onrender.com` |
| `dp-ui` | `VITE_API_BASE_URL` | `https://dp-api-eebu.onrender.com/api/v1` |

After changing `VITE_*`, redeploy the SPA with **Clear build cache** (Vite bakes the value at build time).

**Interview angle:** “Infrastructure-as-code still needs post-create verification of generated hostnames. We treated CORS and SPA API base URL as first-class deploy checklist items.”

---

### Issue 8 — Login UI showed “Invalid email or password” (credentials were valid)

**Symptom**  
Browser login on https://dp-ui-5rd4.onrender.com/login failed with invalid credentials.  
Direct API login with the same credentials returned **200 + JWT**.

**Root cause**  
**CORS**, not auth. API allowed the wrong origin, so the browser blocked the response. The SPA maps *any* login failure (including CORS/network) to the generic “Invalid email or password.” message (no credential enumeration).

**Solution**  
Set `Cors__AllowedOrigins__0` to the exact SPA origin (`https://dp-ui-5rd4.onrender.com`), redeploy API, hard-refresh the UI.

**Interview angle:** “We diagnosed with curl against the live API vs browser behaviour. Same credentials succeeded server-side; missing `Access-Control-Allow-Origin` explained the UI failure. Also a product note: generic client errors can hide infra issues — check Network tab / CORS first.”

---

### Issue 9 — AI extract returned 500 (ASP.NET record validation)

**Symptom**  
`POST /api/v1/ai/extract-dispute` returned **500** with:
```text
InvalidOperationException: Record type 'ExtractDisputeRequest' has validation
metadata defined on property 'Text' that will be ignored.
```

**Root cause**  
Validation attributes were placed with `[property: Required]` on C# primary-constructor record parameters. ASP.NET Core ignores that for model validation and throws at runtime.

**Solution**  
Put attributes directly on the constructor parameters (`[Required] string Text`, etc.) on AI and dispute request DTOs.

**Interview angle:** “Framework-specific pitfall: record primary constructors + DataAnnotations need the attribute on the parameter, not `[property:]`, or the endpoint blows up before business logic runs.”

---

### Issue 10 — Gemini returned 404 for `gemini-2.5-flash` (extract → 502)

**Symptom**  
After the key was configured, extract still failed:
```text
Gemini returned 404 for model gemini-2.5-flash … (transient=false)
HTTP POST /api/v1/ai/extract-dispute → 502
```

**Root cause**  
The free-tier API key / `v1beta` combo did not resolve the configured model id (`gemini-2.5-flash`) — Google returned **404 Not Found** for that model path. Middleware correctly mapped the LLM failure to **502** (not a crash).

**Solution**  
Hardened `GeminiClient` to, on **404**, fall through free-tier flash aliases until one succeeds:
1. configured model (`Gemini:ExtractionModel` / classification / summary)
2. `gemini-flash-latest`
3. `gemini-2.5-flash`
4. `gemini-2.5-flash-lite`

Also send the API key as `?key=` on the request (avoids header quirks) and keep timeouts generous for free-tier latency.

**Interview angle:** “Don’t hard-bind a single model string in production. Treat model availability as an infra concern — fallback on 404, fail closed with 502, keep submit/classification paths resilient.”

---

## 3. Timeline of health recovery

1. Blueprint created → Redpanda image fetch failed → Docker Hub image.  
2. Anthropic blocked → Gemini swap.  
3. Redpanda `--mode` → `rpk redpanda start`.  
4. Redpanda OOM → `--memory 400M`.  
5. Postgres health 503 → normalize connection string in health checks.  
6. Kafka topics / broker → Redpanda Live + longer topic ensure + API redeploy.  
7. Cross-URL suffixes → fix CORS + `VITE_API_BASE_URL`.  
8. Login fails in browser → CORS origin fix → **login success**.  
9. AI extract 500 → fix DataAnnotations on record DTOs.  
10. Gemini model 404 → free-tier flash fallback chain → **extract success**.

**Verified healthy response:**
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgres", "status": "Healthy" },
    { "name": "kafka", "status": "Healthy",
      "description": "Kafka reachable at dp-redpanda:9092 (1 broker(s))." }
  ]
}
```

---

## 4. Suggested demo script

1. Open UI → log in as **Maya** (`maya@example.com` / `Password123!`).  
2. Browse transactions → open one → **Dispute this transaction**.  
3. Natural-language tab — e.g. *“I was charged R450 twice at Shoprite but only shopped once”* → review AI extraction → submit → note reference.  
4. Log out → log in as **Sipho** (`sipho@capitec.ops`) → ops queue → open dispute → **Resolve** → **Generate summary** → confirm.  
5. Back as Maya → see resolution.  
6. Optional: **Zanele** (`zanele@capitec.ops`) → dashboard metrics.  
7. Optional: show Swagger + `/health/ready` as ops evidence.

---

## 5. Interview talking points (concise)

1. **Full-fidelity cloud demo** — kept Kafka/async classification, not a stripped sync-only demo.  
2. **IaC with Blueprints** — `render.yaml` as source of truth; still verified live hostnames.  
3. **Provider swap under pressure** — Gemini vs Anthropic without rewriting domain services.  
4. **Debugging method** — logs → isolate layer (image registry / CLI / memory / connection string / CORS / model id) → smallest fix → redeploy.  
5. **Security hygiene** — LLM key server-side only; CORS allow-list; JWT auth; seed passwords never logged.  
6. **Graceful degradation** — classification consumer retries; AI failures map to 502 / `CLASSIFICATION_FAILED` rather than crashing submit.  
7. **Honest trade-offs** — Starter memory limits, free Gemini rate limits / model alias quirks, Render name suffixes, paid private services for Kafka.

---

## 6. Quick reference — critical env vars

| Key | Service | Notes |
|---|---|---|
| `ConnectionStrings__Default` | API | From Render Postgres (`postgres://` URL; normalised in app) |
| `Kafka__BootstrapServers` | API | `dp-redpanda:9092` |
| `Jwt__Secret` | API | Auto-generated (≥ 32 bytes) |
| `Gemini__ApiKey` | API | Google AI Studio key |
| `Cors__AllowedOrigins__0` | API | Exact SPA origin |
| `VITE_API_BASE_URL` | UI | `https://<api-host>/api/v1` (build-time) |
| `PORT` | API | `8080` |
| `ASPNETCORE_ENVIRONMENT` | API | `Docker` (enables Swagger) |

---

## 7. Related repo docs

- `DEPLOY-RENDER.md` — operational runbook  
- `render.yaml` — Blueprint definition  
- `SPEC.md` — product/architecture specification  
- `batch-planning.md` — delivery batches  
- `DEMO-SCRIPT.md` — live walkthrough script  

---

*Last updated: 22 July 2026 — after Gemini model-fallback fix and successful NL extract on Render.*
