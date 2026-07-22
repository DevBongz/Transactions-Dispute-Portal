# Deploying to Render (Option B — full fidelity)

This deploys the whole stack on Render: managed **Postgres**, a single-node **Redpanda**
(Kafka-compatible) private service, the **.NET API** (Docker), and the **React SPA** (static
site). It keeps the async AI classification/triage feature working.

Everything is defined in [`render.yaml`](./render.yaml) so most of the wiring is automated.

## Prerequisites

- The repo is on GitHub (done) and connected to your Render account.
- A free **Google Gemini API key** from [Google AI Studio](https://aistudio.google.com/apikey).
- A Render workspace. For a smooth live demo, paid **Starter** instances are used in the
  Blueprint (free web services cold-start ~30–60 s; free Postgres expires ~30 days).

## 1. Create the Blueprint

1. Render Dashboard → **New → Blueprint**.
2. Select this repository and branch `main`. Render reads `render.yaml`.
3. When prompted for `sync: false` values, paste your **`Gemini__ApiKey`**.
4. Click **Apply**. Render creates: `dp-postgres`, `dp-redpanda`, `dp-api`, `dp-ui`.

On first boot the API automatically runs EF migrations and seeds demo data.

## 2. Fix the two cross-referencing URLs (only if names got a suffix)

The API needs the SPA's URL (CORS) and the SPA needs the API's URL (`VITE_API_BASE_URL`). The
Blueprint hardcodes the expected `onrender.com` URLs based on the service names:

- `dp-api` → `https://dp-api.onrender.com`
- `dp-ui`  → `https://dp-ui.onrender.com`

If either name was already taken in your workspace, Render appends a random suffix. In that case:

- **`dp-api`** service → env `Cors__AllowedOrigins__0` → set to your actual SPA URL → Save.
- **`dp-ui`** service → env `VITE_API_BASE_URL` → set to `https://<actual-api>.onrender.com/api/v1`
  → **Manual Deploy → Clear build cache & deploy** (Vite bakes this in at build time).

If the names were free, no change is needed.

## 3. Verify

- `https://<api>.onrender.com/health/ready` → `Healthy`.
- `https://<api>.onrender.com/swagger` → API explorer.
- Open `https://<ui>.onrender.com` and log in.

## Demo logins (password for all: `Password123!`)

| Role | Email |
|---|---|
| Customer | `maya@example.com` |
| Ops analyst | `sipho@capitec.ops` |
| Ops manager | `zanele@capitec.ops` |

**Suggested demo flow:** log in as Maya → open a transaction → *Dispute this transaction* → use
the NL tab (*"I was charged R450 twice at Shoprite on 14 July but only shopped once"*) → review
the extracted fields → submit → note the reference. Then log in as Sipho → work the priority
queue → open the dispute → **Resolve** → **Generate summary** → **Confirm**. Log back in as Maya
to see the resolution; log in as Zanele for the metrics.

## Environment variables (reference)

| Key | Where | Value |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | api | `Docker` (enables Swagger) |
| `PORT` | api | `8080` |
| `ConnectionStrings__Default` | api | auto from `dp-postgres` (URL, normalised at startup) |
| `Kafka__BootstrapServers` | api | `dp-redpanda:9092` |
| `Jwt__Secret` | api | auto-generated |
| `Gemini__ApiKey` | api | your Google AI Studio key (prompted) |
| `Cors__AllowedOrigins__0` | api | SPA URL |
| `VITE_API_BASE_URL` | ui | `https://<api>.onrender.com/api/v1` |

Optional AI overrides (set on `dp-api` if a model id is unavailable): `Gemini__ExtractionModel`,
`Gemini__ClassificationModel`, `Gemini__SummaryModel` (default: `gemini-2.0-flash`).

## Troubleshooting

- **Blueprint error `services[0].image … could not be fetched`:** the Redpanda image must come
  from Docker Hub (`docker.io/redpandadata/redpanda:…`). Do not use `docker.redpanda.com`.
- **API unhealthy / DB errors:** confirm `dp-postgres` is in the **same region** (`oregon`) as
  `dp-api`; the connection string is wired automatically via the Blueprint.
- **Disputes fail on submit/resolve (500):** the API can't reach Kafka. Check `dp-redpanda` is
  live and that `Kafka__BootstrapServers` is exactly `dp-redpanda:9092`. If logs show
  `insufficient physical memory`, lower `--memory` (Starter has ~500 MB usable; use `400M`)
  or bump the plan to **standard**.
- **Category/priority stuck on "Pending":** the classification consumer isn't consuming — same
  Kafka connectivity check as above.
- **AI calls return 502:** check `Gemini__ApiKey` is set; if the model id is unavailable, set the
  `Gemini__*Model` overrides above (try `gemini-1.5-flash` or `gemini-2.5-flash`).
- **SPA loads but API calls fail (CORS/404):** re-check step 2 (the two URLs) and redeploy the
  SPA after changing `VITE_API_BASE_URL`.
