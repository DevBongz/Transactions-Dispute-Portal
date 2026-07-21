# TDP-AI-03 — Resolution Summary Generation Endpoint

**Jira summary:** Build the ops-only `POST /api/v1/ai/generate-summary` endpoint that turns an analyst's internal resolution notes into a clear, empathetic, plain-language customer-facing summary (2–4 sentences) using the Anthropic Claude API (`claude-sonnet-5`) per SPEC §3.5 Feature 3. This saves analysts (persona Sipho) from writing separate customer communications and guarantees every resolved dispute carries a plain-language explanation for the customer (persona Maya). The endpoint returns the generated summary for preview/edit in the resolve modal (TDP-FE-05) before it is persisted at resolution time by `POST /api/v1/disputes/{id}/resolve`.

## 1. Context & Motivation

- **Background:** Dispute resolution (`POST /api/v1/disputes/{id}/resolve`, TDP-DISP-03) accepts an `outcome`, `internalNotes`, and a `customerSummary`, persisting a `Resolution` row and publishing `dispute.resolved`. Today the analyst would have to hand-write `customerSummary`. SPEC §2.2 Journey 2 step 8 has the analyst click **Generate Summary**, which calls an AI endpoint and shows a preview. This ticket provides that endpoint. It is the higher-quality customer-facing generation task, so it uses `claude-sonnet-5` rather than haiku (SPEC §3.5 Feature 3).
- **Business Impact:** Serves the objective *"Customers receive clear resolution communication — auto-generated plain-language summaries delivered on every resolved dispute"* (SPEC §1.1) and the ops-efficiency user story OPS-05 (analyst saves time on written communication). It backs Journey 2 (ops resolves a dispute) and Journey 3 (customer reads the resolution summary, TRACK-03).
- **User Story:** As an ops analyst (Sipho), I want the system to generate a plain-language resolution summary for the customer from my internal resolution notes so that I save time on written communication and the customer gets a clear explanation of the outcome (AI-03 / OPS-05).
- **Dependencies:** Depends on **TDP-DISP-03** (resolution flow, `Resolution` entity, `dispute.resolved`) for the surrounding context and persistence target, and on **TDP-AUTH-01** for role-based authorization. Reuses the `IAnthropicClient` typed HttpClient from **TDP-AI-01**. Consumed by **TDP-FE-05** (resolve modal preview) and documented by **TDP-DOC-01**. Milestone: **Day 3 — AI Integration** (SPEC §4.1). This endpoint **generates and returns** the summary only; persistence happens when the analyst confirms resolution via TDP-DISP-03.

## 2. Detailed Description

### 2.1 Endpoint contract (SPEC §3.3, AI Endpoints)

| Method | Path | Auth | Request Body | Response |
|---|---|---|---|---|
| POST | `/api/v1/ai/generate-summary` | Bearer JWT, **ops roles only** (`OPS_ANALYST`, `OPS_MANAGER`) | `{ disputeId, outcome, internalNotes }` | `200 { "summary": string }` |

Example request:

```json
{
  "disputeId": "3f9a...-uuid",
  "outcome": "UPHELD",
  "internalNotes": "Transaction confirmed as duplicate — refund of R450 initiated via settlement-processor."
}
```

Example `200 OK`:

```json
{
  "summary": "We've reviewed your dispute and confirmed you were charged twice for the same R450 purchase at Shoprite on 14 July 2026. We've upheld your dispute and a refund of R450 is being processed back to your account. You don't need to take any further action."
}
```

### 2.2 File layout (extends the AI feature set)

```
src/DisputePortal.Api/
├── Controllers/AiController.cs               # add [HttpPost("generate-summary")]
├── Services/Ai/
│   ├── IResolutionSummaryService.cs
│   ├── ResolutionSummaryService.cs           # builds prompt, calls claude-sonnet-5
│   └── Prompts/SystemPrompts.cs              # + ResolutionSummary prompt (SPEC §3.5)
├── Contracts/Ai/
│   ├── GenerateSummaryRequest.cs             # disputeId, outcome, internalNotes
│   └── GenerateSummaryResponse.cs            # summary
```

### 2.3 Authorization (ops-only)

Per SPEC §3.3 the endpoint is ops-only and §2.1 restricts summary generation to analysts. Enforce with role-based policy from TDP-AUTH-01:

```csharp
[HttpPost("generate-summary")]
[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
[ProducesResponseType(typeof(GenerateSummaryResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status502BadGateway)]
public async Task<IActionResult> GenerateSummary(
    [FromBody] GenerateSummaryRequest request, CancellationToken ct) { ... }
```

A `CUSTOMER` token receives `403`.

### 2.4 Context assembly

The service loads the `Dispute` + related `Transaction` by `disputeId` to build the model's user message with real transaction detail (amount, merchant, date) and the dispute `reference`, rather than trusting only the request body. A `disputeId` that does not exist (or is out of the caller's ops scope) → `404`. `outcome` must be one of `UPHELD`, `DECLINED`, `PARTIAL` (the `Resolution.outcome` enum, SPEC §3.2); `internalNotes` minimum 20 characters (AC-OPS-04). Invalid `outcome` or too-short notes → `400`.

### 2.5 System prompt (verbatim from SPEC §3.5, Feature 3)

Model: **`claude-sonnet-5`** (higher quality for customer-facing text). `SystemPrompts.ResolutionSummary`:

```
You are a customer communication specialist at a bank. Write a clear, empathetic, plain-language summary (2–4 sentences) for the customer explaining the outcome of their transaction dispute.
Do not use jargon. Do not reveal internal investigation details. Be specific about the outcome.
Return only the summary text, no JSON wrapper.
```

User message (SPEC §3.5 Feature 3), assembled from the loaded dispute/transaction + request:

```
Dispute reference: DSP-20260714-00042
Transaction: R450.00 at Shoprite on 14 July 2026
Outcome: UPHELD
Internal notes: "Transaction confirmed as duplicate — refund of R450 initiated."
```

### 2.6 Response handling

- Unlike extraction/classification, the model returns **plain text, not JSON** (system prompt: "Return only the summary text, no JSON wrapper"). Read `content[0].text`, trim it, and return it as `{ "summary": "<text>" }`.
- Guard: if the model output is empty or the response has no text block, return `502`.
- The summary is **not persisted here** — it is returned for preview. The analyst may edit it in the resolve modal (Journey 2 step 9) and the final text is written to `Resolution.customer_summary` by `POST /api/v1/disputes/{id}/resolve` (TDP-DISP-03). This keeps generation idempotent and side-effect-free.

### 2.7 Anthropic client reuse

Reuse `IAnthropicClient` from TDP-AI-01. Add to `AnthropicOptions`:

```csharp
public string SummaryModel { get; set; } = "claude-sonnet-5";
public int SummaryMaxTokens { get; set; } = 512; // 2–4 sentences fits comfortably
```

Request body to `POST /v1/messages`:

```jsonc
{
  "model": "claude-sonnet-5",
  "max_tokens": 512,
  "system": "<SystemPrompts.ResolutionSummary>",
  "messages": [ { "role": "user", "content": "<assembled context from §2.5>" } ]
}
```

> **Model ID note:** use the exact string `claude-sonnet-5` as pinned in SPEC §3.5 / §4.2 — do not append a date suffix.

### 2.8 Error handling & security

- Anthropic non-2xx / timeout / empty output → `502 Bad Gateway` (`{"error":"summary_unavailable"}`); the analyst can still type a summary manually in the modal, so failure is non-blocking to resolution.
- `ANTHROPIC_API_KEY` server-side only (SPEC §3.6); never returned or logged.
- The prompt instructs the model not to reveal internal investigation details; the endpoint additionally never echoes `internalNotes` back in the response — only the generated `summary`.

## 3. Acceptance Criteria

From SPEC §2.3 (AC-OPS-04 / AC-AI-03) and §3.6:

- The AI-generated summary is a **plain-language paragraph of 2–4 sentences** explaining the outcome (AC-AI-03), returned as `{ "summary": string }` (SPEC §3.3).
- The endpoint is **ops-only**: `OPS_ANALYST`/`OPS_MANAGER` succeed; a `CUSTOMER` token gets `403`; no token gets `401`.
- `outcome` is validated against `UPHELD`/`DECLINED`/`PARTIAL`; `internalNotes` must be ≥ 20 characters (AC-OPS-04); invalid input → `400`. Unknown `disputeId` → `404`.
- Model used is `claude-sonnet-5` (verifiable via request payload / structured log field `ai.model`).
- The summary text is returned for preview/edit and is **not** persisted by this endpoint; the confirmed text is stored in `Resolution.customer_summary` by TDP-DISP-03 and shown to the customer on the dispute detail page once resolved (AC-AI-03, TRACK-03).
- On Anthropic failure/timeout the endpoint returns `502` with no stack trace or API key; the analyst can still enter a summary manually.
- The generation call, model, duration, and outcome are logged via Serilog with the correlation ID (TDP-OBS-01); the API key is never logged.

## 4. Technical Notes

- **Model ID exact:** `claude-sonnet-5` (SPEC §3.5 Feature 3 — chosen for higher quality customer-facing prose). Do not substitute haiku or add a date suffix.
- **Plain-text output:** unlike TDP-AI-01/02, do **not** JSON-parse — the summary is raw text in `content[0].text`. Still guard `content.Length > 0` before indexing.
- **`max_tokens`:** 512 is generous for 2–4 sentences; keeps latency and cost low. No streaming needed (single synchronous request).
- **Latency:** sonnet is slower than haiku but this is an interactive ops action, not on the customer submission path; a reasonable timeout (e.g. 10–15s) is acceptable here since SPEC's 5s target applies to extraction/classification, not summary generation. Surface a timeout as `502`.
- **Config:** `Anthropic:SummaryModel`, `Anthropic:SummaryMaxTokens`; `Anthropic__ApiKey` from `ANTHROPIC_API_KEY` (SPEC §3.1).
- **Authorization:** reuse the role policies established in TDP-AUTH-01 (`[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]`). Verify the enum casing matches the seeded roles (`OPS_ANALYST`, `OPS_MANAGER`, SPEC §3.2).
- **No side effects:** the endpoint is a pure generation call — no DB writes, no Kafka publish. Persistence and the `dispute.resolved` event belong to TDP-DISP-03. This separation lets the analyst regenerate/edit freely before confirming.
- **Testing (SPEC §4.4):** integration test asserts the `{ summary }` shape with a **mocked Anthropic HTTP client**; unit test covers role enforcement (403 for customer), input validation (400 for short notes / bad outcome), 404 for unknown dispute, and 502 on Anthropic failure. `OpsResolveModal` frontend test (TDP-TEST-02) depends on this endpoint's contract.
- **Security:** API key server-side only; `internalNotes` are internal and must not leak — only the generated `summary` is returned. The system prompt already forbids revealing investigation detail.

## 5. Definition of Done

- [ ] `AiController.GenerateSummary` implemented behind `[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]`, returning `{ "summary": string }` (SPEC §3.3).
- [ ] `ResolutionSummaryService` assembles the SPEC §3.5 Feature 3 context (reference, transaction, outcome, notes) and calls `claude-sonnet-5` with the verbatim system prompt.
- [ ] Plain-text output read from `content[0].text` (no JSON parse); empty output → `502`.
- [ ] Validation: `outcome` ∈ {UPHELD, DECLINED, PARTIAL}; `internalNotes` ≥ 20 chars (`400`); unknown `disputeId` → `404`; customer role → `403`; no token → `401`.
- [ ] No DB writes and no Kafka publish from this endpoint; persistence deferred to TDP-DISP-03.
- [ ] Anthropic failure/timeout → `502`; no API key or stack trace in the response; manual entry still possible.
- [ ] Unit + integration tests (role enforcement, validation, mocked-Anthropic happy path, 502 path) green via `dotnet test`.
- [ ] Serilog logs model, duration, correlation ID, outcome; API key confirmed absent from logs/responses.
- [ ] Swagger shows the endpoint with request/response schemas (feeds TDP-DOC-01) and it is reachable at `http://localhost:5000/swagger`.
- [ ] Verified end-to-end: from the resolve modal (or Swagger) a set of internal notes produces a 2–4 sentence customer summary preview. PR reviewed and merged to `main`.
