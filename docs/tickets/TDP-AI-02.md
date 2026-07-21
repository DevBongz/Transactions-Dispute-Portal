# TDP-AI-02 — Dispute Classification Background Consumer

**Jira summary:** Implement an `IHostedService` Kafka consumer that subscribes to the `dispute.submitted` topic, classifies each dispute (category + priority) with the Anthropic Claude API (`claude-haiku-4-5-20251001`) per SPEC §3.5 Feature 2, persists the result on the `Dispute` record, and publishes a `dispute.classified` event. Classification runs asynchronously so ops analysts (persona Sipho) receive pre-triaged cases without the customer waiting. Crucially, any AI failure marks the dispute `CLASSIFICATION_FAILED` for manual triage and never blocks submission (AC-AI-02). This is the auto-triage engine that satisfies the objective "100% of submitted disputes carry an AI-assigned category and priority within 5 seconds."

## 1. Context & Motivation

- **Background:** `POST /api/v1/disputes` (TDP-DISP-01) creates a dispute with `category`/`priority` NULL and publishes `dispute.submitted` (TDP-KAFKA-01). Nothing consumes that topic yet, so disputes sit unclassified. The architecture diagram (SPEC §3.1) specifies a "Dispute Classification Consumer" running as a **Hosted Service within the same .NET process** that subscribes to `dispute.submitted`, calls Claude, and publishes `dispute.classified`.
- **Business Impact:** Realises the measurable objective *"Disputes are auto-classified on receipt — 100% of submitted disputes carry an AI-assigned category and priority within 5 seconds"* (SPEC §1.1) and the ops-throughput objective by delivering pre-triaged cases to the operations dashboard (OPS-01/OPS-02). It closes the async step (9) of User Journey 1.
- **User Story:** As the system, I want every submitted dispute to be automatically classified by category and priority using AI so that ops analysts receive pre-triaged cases and work the most critical ones first (AI-02).
- **Dependencies:** Depends on **TDP-KAFKA-01** (Kafka producer + `Confluent.Kafka` wiring, topic conventions) and **TDP-DISP-01** (`Dispute` entity, `dispute.submitted` schema). Reuses the `IAnthropicClient` typed HttpClient from **TDP-AI-01**. Feeds **TDP-DISP-02** (ops list shows category/priority) and is exercised by **TDP-TEST-01**. Milestone: **Day 3 — AI Integration** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Component layout

```
src/DisputePortal.Api/
├── BackgroundServices/
│   └── DisputeClassificationConsumer.cs   # BackgroundService (IHostedService), consumes dispute.submitted
├── Services/Ai/
│   ├── IDisputeClassificationService.cs
│   ├── DisputeClassificationService.cs     # builds prompt, calls IAnthropicClient, maps result
│   └── Prompts/SystemPrompts.cs            # + Classification prompt (SPEC §3.5)
├── Messaging/
│   ├── Topics.cs                           # "dispute.submitted", "dispute.classified", "dispute.resolved"
│   ├── DisputeSubmittedEvent.cs            # consumed schema (SPEC §3.4)
│   └── DisputeClassifiedEvent.cs           # published schema (SPEC §3.4)
```

### 2.2 Hosted service — consume loop

`DisputeClassificationConsumer : BackgroundService`, registered via `builder.Services.AddHostedService<DisputeClassificationConsumer>();`. It runs inside the same API process (SPEC §3.1).

```csharp
public sealed class DisputeClassificationConsumer : BackgroundService
{
    // Injected: IConsumer<string, string> (or a factory), IServiceScopeFactory,
    // ILogger, IOptions<KafkaOptions>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topics.DisputeSubmitted); // "dispute.submitted"
        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = _consumer.Consume(stoppingToken);      // blocking consume
            using var scope = _scopeFactory.CreateScope();  // scoped DbContext + services
            await HandleAsync(scope, cr.Message.Value, stoppingToken);
            _consumer.Commit(cr);                           // commit AFTER successful handle
        }
    }
}
```

Key wiring points:

- **Consumer group:** `GroupId = "dispute-classification"` so multiple API instances can scale horizontally (SPEC §3.6 Scalability: "GroupId set; multiple instances safe"). `EnableAutoCommit = false` — commit only after the dispute is persisted and `dispute.classified` is published, giving at-least-once processing.
- **Scoped services:** the consumer is a singleton; resolve `DbContext`, `IDisputeRepository`, `IDisputeClassificationService`, and the Kafka producer from a per-message `IServiceScope`.
- **Blocking `Consume` off the thread pool:** run the loop on a long-running background task; wrap `Consume` so cancellation shuts down cleanly on host stop.

### 2.3 Consumed event — `dispute.submitted` (SPEC §3.4)

```json
{
  "eventId": "uuid",
  "occurredAt": "ISO8601",
  "disputeId": "uuid",
  "reference": "DSP-20260714-00042",
  "transactionId": "uuid",
  "customerId": "uuid",
  "category": null,
  "description": "string"
}
```

The handler loads the full `Dispute` + related `Transaction` (merchant, amount, date, `merchant_category`) by `disputeId` to build the classification context; it does not trust denormalised event fields for the write.

### 2.4 Classification service — prompt (verbatim from SPEC §3.5, Feature 2)

Model: **`claude-haiku-4-5-20251001`**. `SystemPrompts.Classification`:

```
You are a financial dispute triage engine. Classify the following dispute.
Return a JSON object: { "category": "<CATEGORY>", "priority": "<PRIORITY>", "rationale": "<one sentence>" }
Category must be one of: UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER.
Priority must be one of: LOW, MEDIUM, HIGH, CRITICAL.
Base priority on: amount (>R5000 = HIGH baseline), category (UNAUTHORISED = bump one level), and any prior open disputes by this customer.
Return only valid JSON.
```

User message context (SPEC §3.5 "Context injected into user message"):

```
Transaction: { merchant, amount, date, merchantCategory }
Customer description: "<text>"
Customer open dispute count: <n>
```

The service computes `Customer open dispute count` from the repository (count of the customer's disputes in `OPEN`/`UNDER_REVIEW`, excluding the current one) and injects it so the model can apply the "prior open disputes" rule.

### 2.5 Category + priority mapping and persistence

- Deserialize `content[0].text` with `System.Text.Json`; validate `category` ∈ {`UNAUTHORISED`,`DUPLICATE_CHARGE`,`MERCHANT_ERROR`,`WRONG_AMOUNT`,`OTHER`} and `priority` ∈ {`LOW`,`MEDIUM`,`HIGH`,`CRITICAL`} (AC-AI-02).
- On success: set `Dispute.category`, `Dispute.priority`, `Dispute.updated_at`; append a `DisputeEvent` of type `CLASSIFIED` (system event, `actor_id` NULL) with a human-readable description incorporating the model `rationale`. Status transitions `OPEN → UNDER_REVIEW` is **not** performed here (that is an ops action, OPS/TRACK flow) — status stays as set at submission unless it was `CLASSIFICATION_FAILED` being retried.
- Persist via EF Core (`MigrateAsync` schema from TDP-DATA-01). Columns: `category VARCHAR(50)`, `priority VARCHAR(20)`.

### 2.6 Publish `dispute.classified` (SPEC §3.4)

After a successful persist, publish to `Topics.DisputeClassified` using the TDP-KAFKA-01 producer, keyed by `disputeId`:

```json
{
  "eventId": "uuid",
  "occurredAt": "ISO8601",
  "disputeId": "uuid",
  "reference": "string",
  "category": "DUPLICATE_CHARGE",
  "priority": "HIGH",
  "classifiedBy": "claude-haiku-4-5-20251001"
}
```

`classifiedBy` is the exact model ID string.

### 2.7 CLASSIFICATION_FAILED fallback (AC-AI-02, non-blocking)

This is the reliability core of the ticket (SPEC §3.6: "AI classification failure does not block dispute submission"):

- If the Anthropic call errors, times out, returns unparseable JSON, or returns an out-of-set `category`/`priority` after a bounded retry, set `Dispute.status = 'CLASSIFICATION_FAILED'`, leave `category`/`priority` NULL, append a `DisputeEvent` describing the failure, and **still commit the Kafka offset** so the consumer does not loop on a poison message. Do **not** publish `dispute.classified`.
- Submission (`POST /api/v1/disputes`) already returned `201` before this consumer ran, so the customer is never blocked — the failure is contained to the async lane and surfaced for manual triage in the ops dashboard (`CLASSIFICATION_FAILED` is a filterable/visible status per the `Dispute.status` enum in SPEC §3.2).
- Retry policy: at most one immediate retry within a total budget of ~5s (see §3 timing) before falling back; transient 429/5xx get the single retry, deterministic parse errors do not.

## 3. Acceptance Criteria

From SPEC §2.3 (AC-AI-02) and §3.6:

- Every dispute is classified **within 5 seconds of the `dispute.submitted` Kafka event** being consumed (AC-AI-02; NFR "AI classification (async, background) < 5 seconds after Kafka event consumed"). The Anthropic call uses a ≤5s timeout budget.
- `category` is one of `UNAUTHORISED`, `DUPLICATE_CHARGE`, `MERCHANT_ERROR`, `WRONG_AMOUNT`, `OTHER`; `priority` is one of `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`.
- The classification result is stored on the `Dispute` record (`category`, `priority`, `updated_at`) and is visible in the ops dashboard (AC-AI-02; consumed by TDP-DISP-02).
- **If the AI call fails, the dispute is flagged `CLASSIFICATION_FAILED` and surfaced for manual triage; the submission is not blocked** (AC-AI-02). The Kafka offset is still committed (no poison-message loop) and no `dispute.classified` event is published.
- On success a `dispute.classified` event is published to the `dispute.classified` topic with the SPEC §3.4 schema, including `classifiedBy: "claude-haiku-4-5-20251001"`.
- A `DisputeEvent` of type `CLASSIFIED` (or a failure event) is appended, providing the timeline entry for TRACK-02.
- The consumer uses a fixed `GroupId` so multiple API instances are safe to run concurrently (SPEC §3.6 Scalability).
- Consume/publish activity is logged with topic, partition, offset, correlation ID, and outcome (SPEC §3.6 Observability).

## 4. Technical Notes

- **Model ID exact:** `claude-haiku-4-5-20251001` for classification (SPEC §3.5 Feature 2). Reuse `IAnthropicClient` from TDP-AI-01; add a `ClassificationModel`/`ClassificationMaxTokens` to `AnthropicOptions` (256–512 tokens is enough for the small JSON).
- **Confluent.Kafka config:** `BootstrapServers` from `Kafka__BootstrapServers` (`kafka:9092` in compose, SPEC §3.1); `GroupId = "dispute-classification"`, `EnableAutoCommit = false`, `AutoOffsetReset = Earliest`. Topics auto-created (`KAFKA_AUTO_CREATE_TOPICS_ENABLE=true`, SPEC §3.1/§4.3).
- **At-least-once semantics:** commit offset only after DB persist + publish (success path) or after marking `CLASSIFICATION_FAILED` (failure path). Because re-delivery is possible, make the handler idempotent — if the dispute already has a non-null `category`/`priority` (or a `CLASSIFIED` event exists), skip re-classification.
- **Scoping:** the hosted service is a singleton; never inject a scoped `DbContext` directly — resolve per message via `IServiceScopeFactory`. This is the most common bug in this pattern.
- **Graceful shutdown:** honour `stoppingToken`; close/dispose the consumer so rebalances are clean on container stop (matters for `docker compose down`).
- **Startup ordering:** Kafka may not be ready when the API starts; the consume loop should catch broker-unavailable and retry with backoff rather than crashing the host (SPEC §4.3 risk: Kafka setup complexity).
- **Testing (SPEC §4.4):** `AiClassificationService.ClassifyAsync` unit test mocks the Anthropic HTTP client and asserts the category/priority mapping and the `CLASSIFICATION_FAILED` fallback on error. TDP-TEST-01 covers the consumer end-to-end with Testcontainers (Postgres) and a mocked Anthropic handler.
- **Security:** `ANTHROPIC_API_KEY` server-side only; never logged (SPEC §3.6). The `rationale` is internal — safe to store in `DisputeEvent.description` but it is not customer-facing.

## 5. Definition of Done

- [ ] `DisputeClassificationConsumer` (`BackgroundService`) registered via `AddHostedService`, subscribed to `dispute.submitted` with `GroupId = "dispute-classification"` and manual commit.
- [ ] `DisputeClassificationService` builds the SPEC §3.5 Feature 2 prompt (verbatim system prompt + injected transaction/description/open-count context) and calls `claude-haiku-4-5-20251001`.
- [ ] Category and priority validated against the allowed sets and persisted to `Dispute` (+ `CLASSIFIED` `DisputeEvent`).
- [ ] `dispute.classified` published on success with the SPEC §3.4 schema and `classifiedBy` model ID.
- [ ] On any AI failure/timeout/parse error: `Dispute.status = CLASSIFICATION_FAILED`, submission unaffected, offset committed, no classified event published (AC-AI-02).
- [ ] Handler is idempotent against Kafka re-delivery.
- [ ] Classification completes within 5s of consuming the event on the happy path.
- [ ] Serilog logs topic/partition/offset, model, duration, and outcome with correlation ID; API key absent from logs.
- [ ] Unit tests (mapping + `CLASSIFICATION_FAILED` fallback) and integration coverage (TDP-TEST-01) green via `dotnet test`.
- [ ] Verified end-to-end in `docker compose up`: submitting a dispute results in a populated category/priority (or `CLASSIFICATION_FAILED`) within 5s and a `dispute.classified` message on the topic.
- [ ] PR reviewed and merged to `main`.
