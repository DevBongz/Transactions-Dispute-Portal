# TDP-AI-01 — Natural Language Dispute Extraction Endpoint

**Jira summary:** Build the `POST /api/v1/ai/extract-dispute` endpoint that turns a customer's free-text description of a transaction problem into structured, pre-populated dispute fields using the Anthropic Claude API (`claude-haiku-4-5-20251001`). The endpoint returns the extracted fields plus a per-field confidence map so the frontend can highlight low-confidence fields for review, delivering the low-friction "describe it in your own words" submission path (DISP-02 / AI-01) that differentiates the portal from a plain structured form. This is the first of the three Day-3 AI features and the backend dependency for the natural-language tab in the dispute submission UI (TDP-FE-03).

## 1. Context & Motivation

- **Background:** The dispute domain (`Dispute` entity, `POST /api/v1/disputes`) and JWT auth are in place from Group A/B, but disputes today can only be raised by filling in every structured field by hand. Persona 1 (Maya, retail customer) wants to describe a problem in plain language — *"I was charged R450 twice at Shoprite on 14 July but I only shopped once"* — and have the system fill in the form. No AI integration exists yet in the codebase; this ticket introduces the first server-side Anthropic Claude client.
- **Business Impact:** Directly serves objective *"Customers can self-serve dispute submissions"* (target ≥ 95% form submission success rate) by lowering the friction of submission. It underpins User Journey 1 (natural-language dispute) and is the backend half of the two-tab submission experience; without it the NL tab in TDP-FE-03 cannot function.
- **User Story:** As a customer (Maya), I want to describe my transaction problem in plain language and have the system extract the reason, amount, merchant, and date automatically, so that I do not have to fill in every field manually and can submit a dispute in seconds.
- **Dependencies:** Depends on **TDP-AUTH-01** (endpoint requires a valid `CUSTOMER` JWT). Consumes the Anthropic Claude API. Consumed by **TDP-FE-03** (NL submission tab) and documented by **TDP-DOC-01**. Milestone: **Day 3 — AI Integration** (SPEC §4.1). No database writes — extraction is a read-only assist step; persistence happens later at `POST /api/v1/disputes`.

## 2. Detailed Description

### 2.1 Endpoint contract

Per SPEC §3.3 (AI Endpoints):

| Method | Path | Auth | Request Body | Response |
|---|---|---|---|---|
| POST | `/api/v1/ai/extract-dispute` | Bearer JWT (any authenticated role; primarily `CUSTOMER`) | `{ "text": "string" }` | `200 { transactionRef?, category?, amount?, merchantName?, transactionDate?, confidence: { [field]: number } }` |

Example request:

```json
{ "text": "I was charged R450 twice at Shoprite on 14 July but I only shopped once." }
```

Example `200 OK` response:

```json
{
  "transactionRef": null,
  "category": "DUPLICATE_CHARGE",
  "amount": 450.00,
  "merchantName": "Shoprite",
  "transactionDate": "2026-07-14",
  "confidence": {
    "category": 0.94,
    "amount": 0.88,
    "merchantName": 0.91,
    "transactionDate": 0.72,
    "transactionRef": 0.0
  }
}
```

### 2.2 Directory / file layout

```
src/DisputePortal.Api/
├── Controllers/
│   └── AiController.cs                  # POST /ai/extract-dispute (this ticket), /ai/generate-summary (TDP-AI-03)
├── Services/
│   ├── Ai/
│   │   ├── IAnthropicClient.cs          # thin wrapper over the Messages API
│   │   ├── AnthropicClient.cs           # typed HttpClient impl
│   │   ├── AnthropicOptions.cs          # bound from "Anthropic" config section
│   │   ├── IDisputeExtractionService.cs
│   │   ├── DisputeExtractionService.cs  # builds prompt, calls client, parses JSON
│   │   └── Prompts/SystemPrompts.cs     # system prompt constants (SPEC §3.5)
├── Contracts/Ai/
│   ├── ExtractDisputeRequest.cs
│   └── ExtractDisputeResponse.cs
```

### 2.3 Anthropic client (typed HttpClient)

SPEC §3.5 permits either the `Anthropic` NuGet package or a raw `HttpClient` against `https://api.anthropic.com/v1/messages`. Use a **typed `HttpClient`** registered via `IHttpClientFactory` — it keeps the dependency surface small, is trivial to mock in integration tests, and is shared by TDP-AI-01/02/03.

`AnthropicOptions` (bound from the `Anthropic` config section; `Anthropic__ApiKey` is injected from `ANTHROPIC_API_KEY` in `docker-compose`, SPEC §3.1):

```csharp
public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string AnthropicVersion { get; set; } = "2023-06-01";
    public string ExtractionModel { get; set; } = "claude-haiku-4-5-20251001";
    public int ExtractionMaxTokens { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 5; // per AC-DISP-02 / NFR
}
```

Registration in `Program.cs`:

```csharp
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.AddHttpClient<IAnthropicClient, AnthropicClient>((sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    http.BaseAddress = new Uri(opts.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    http.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
    http.DefaultRequestHeaders.Add("anthropic-version", opts.AnthropicVersion);
});
builder.Services.AddScoped<IDisputeExtractionService, DisputeExtractionService>();
```

The client issues `POST /v1/messages`. Wire shape (from the Anthropic Messages API):

```jsonc
// Request body
{
  "model": "claude-haiku-4-5-20251001",
  "max_tokens": 1024,
  "system": "<system prompt from §2.4>",
  "messages": [
    { "role": "user", "content": "<raw customer text>" }
  ]
}
```

```jsonc
// Response body — the assistant text is in content[0].text
{
  "id": "msg_...",
  "model": "claude-haiku-4-5-20251001",
  "stop_reason": "end_turn",
  "content": [ { "type": "text", "text": "{ \"category\": \"DUPLICATE_CHARGE\", ... }" } ],
  "usage": { "input_tokens": 120, "output_tokens": 60 }
}
```

> **Model ID note:** use the exact string `claude-haiku-4-5-20251001` as pinned in SPEC §3.5 / §4.2 — do not shorten it or add a different date suffix.

### 2.4 System prompt (verbatim from SPEC §3.5, Feature 1)

Stored as a constant in `SystemPrompts.Extraction`:

```
You are a dispute intake assistant for a bank. Extract structured dispute fields from the customer's description.
Return a JSON object with these optional fields: transactionRef, category (one of UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER), amount (number), merchantName, transactionDate (ISO8601 date), and a confidence map (0.0–1.0 per field).
If a field cannot be determined, omit it. Return only valid JSON.
```

The raw customer `text` is passed unchanged as the single `user` message (SPEC §3.5: "User message: The raw customer text").

### 2.5 Response handling & parsing

- The assistant returns a JSON object as `content[0].text`. Deserialize it with `System.Text.Json` into an intermediate DTO, then map to `ExtractDisputeResponse`.
- **Do not raw-string-match** the model output — always `JsonSerializer.Deserialize`. Guard against the model wrapping JSON in prose: extract the first `{ ... }` span if `stop_reason != "end_turn"` or the body does not start with `{`.
- `category` must be validated against the allowed set (`UNAUTHORISED`, `DUPLICATE_CHARGE`, `MERCHANT_ERROR`, `WRONG_AMOUNT`, `OTHER`); an out-of-set value is dropped (field omitted, confidence 0).
- Any field absent from the model output is returned as `null` with confidence `0.0`.
- **Confidence < 0.6 handling (SPEC §3.5):** the backend returns the raw confidence map faithfully; per AC-DISP-02 the *frontend* leaves low-confidence fields blank with a placeholder. The backend must therefore always populate the `confidence` map for every field name it returns so TDP-FE-03 can apply the `< 0.6` rule. Include a key for every response field even when the field value is null.

### 2.6 Controller

```csharp
[ApiController]
[Route("api/v1/ai")]
[Authorize] // any authenticated user; NL extraction is a customer flow
public sealed class AiController : ControllerBase
{
    private readonly IDisputeExtractionService _extraction;

    [HttpPost("extract-dispute")]
    [ProducesResponseType(typeof(ExtractDisputeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ExtractDispute(
        [FromBody] ExtractDisputeRequest request, CancellationToken ct)
    {
        // validate, call _extraction.ExtractAsync(request.Text, ct), return Ok(result)
    }
}
```

### 2.7 Error handling & resilience

- Empty/whitespace `text`, or `text` longer than a configured cap (e.g. 4000 chars) → `400 Bad Request` with a validation problem-details body; no Anthropic call is made.
- Anthropic non-2xx, timeout (`TaskCanceledException`), or unparseable JSON → `502 Bad Gateway` with a generic message (`{"error":"extraction_unavailable"}`). The customer can still submit via the structured form, so extraction failure is non-fatal to the journey.
- The `ANTHROPIC_API_KEY` is read server-side only (SPEC §3.6 Security) — it must never appear in a response body, log line, or error message.

## 3. Acceptance Criteria

Pulled from SPEC §2.3 (AC-DISP-02) and §3.6 (NFR):

- Given a plain-text description containing a merchant name, amount, and reason, the endpoint returns extracted fields **within 5 seconds** (AC-DISP-02; NFR "AI extraction endpoint response time < 5 seconds"). The `HttpClient` timeout is set to 5s to enforce this.
- The response body matches the SPEC §3.3 shape: optional `transactionRef`, `category`, `amount`, `merchantName`, `transactionDate`, plus a `confidence` map with a numeric `0.0–1.0` value per field.
- `category`, when present, is one of `UNAUTHORISED`, `DUPLICATE_CHARGE`, `MERCHANT_ERROR`, `WRONG_AMOUNT`, `OTHER`; any other value is dropped.
- Fields the model cannot determine are omitted/`null` and carry a confidence entry so the UI can leave them blank with a placeholder (AC-DISP-02).
- The endpoint returns `401` without a valid JWT (AC-AUTH-01 chain) and `400` for empty/oversized `text`.
- On Anthropic failure/timeout the endpoint returns `502` (not `500`), the response contains no stack trace or API key, and submission via the structured form remains possible.
- Model used is `claude-haiku-4-5-20251001` (verifiable via the request payload / structured log field `ai.model`).
- The call, its duration, and outcome are logged via Serilog with the correlation ID (TDP-OBS-01), and the API key is never logged.

## 4. Technical Notes

- **Config keys:** `Anthropic:ApiKey` (from `ANTHROPIC_API_KEY`), `Anthropic:ExtractionModel`, `Anthropic:TimeoutSeconds`. In `docker-compose`, `Anthropic__ApiKey: "${ANTHROPIC_API_KEY}"` (SPEC §3.1).
- **Model ID is exact:** `claude-haiku-4-5-20251001`. Do not substitute a shorter alias or a different date.
- **Anthropic Messages API:** `POST /v1/messages`; required headers `x-api-key`, `anthropic-version: 2023-06-01`, `content-type: application/json`. `max_tokens` is required; 1024 is ample for the extraction JSON. Assistant output is in `content[0].text`.
- **JSON robustness:** the model is instructed to "Return only valid JSON", but code defensively — trim, locate the first balanced `{...}`, and `JsonSerializer.Deserialize` with `PropertyNameCaseInsensitive = true`. Never index into `content` without a length check.
- **Latency budget:** haiku is chosen precisely for low latency (SPEC §3.5). Keep `max_tokens` small; do not enable streaming (single synchronous request). The 5s `HttpClient` timeout is the enforced ceiling — surface a timeout as `502`.
- **Security:** API key server-side only (SPEC §3.6). Register the header on the typed client, not per-request, so it is never accidentally serialized. Add a Serilog destructuring policy or explicit `[[LogMasked]]`-style guard to keep `ApiKey` out of logs.
- **Testing (per SPEC §4.4):** integration test `POST /api/v1/ai/extract-dispute` asserts response shape with a **mocked Anthropic HTTP client** (inject a stub `HttpMessageHandler` returning a canned `messages` response) — no live API calls in CI.
- **Rate limits (SPEC §4.3):** treat Anthropic 429 as a `502` to the caller; do not retry aggressively inside the 5s budget.

## 5. Definition of Done

- [ ] `IAnthropicClient` / `AnthropicClient` typed `HttpClient` registered and reading config from the `Anthropic` section.
- [ ] `AiController.ExtractDispute` implemented behind `[Authorize]`, returning the SPEC §3.3 response shape.
- [ ] System prompt matches SPEC §3.5 Feature 1 verbatim; model is `claude-haiku-4-5-20251001`.
- [ ] Confidence map returned for every field; validation and category-allow-list enforced.
- [ ] `400` for empty/oversized input; `502` for Anthropic failure/timeout; `401` without JWT.
- [ ] 5s timeout wired and verified; happy-path returns within budget.
- [ ] Unit tests (JSON parsing, category validation, timeout → 502) and an integration test with a mocked Anthropic handler are green (`dotnet test`).
- [ ] Serilog logs the model, duration, correlation ID, and outcome; API key confirmed absent from logs and responses.
- [ ] Swagger shows the endpoint with request/response schemas (feeds TDP-DOC-01).
- [ ] Endpoint reachable via Swagger UI at `http://localhost:5000/swagger`; PR reviewed and merged to `main`.
