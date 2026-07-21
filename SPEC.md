# Transactions Dispute Portal — Project Specification

**Project:** Option 2 — Transactions Dispute Portal  
**Prepared by:** Bongani Duma  
**Submission deadline:** 23 July 2026  
**Last updated:** 16 July 2026  

---

## Table of Contents

1. [Project Overview & Context](#1-project-overview--context)
2. [User & Business Requirements](#2-user--business-requirements)
3. [Technical Specifications](#3-technical-specifications)
4. [Project Management & Delivery](#4-project-management--delivery)

---

## 1. Project Overview & Context

### 1.1 Background & Objectives

The Transactions Dispute Portal is a full-stack web application that enables customers to view their transaction history, raise disputes against individual transactions, and track the lifecycle of those disputes through to resolution. Operations staff can triage, investigate, and resolve disputes from the same system.

The portal is built in the context of **DMC Fin-Motion** — Capitec Bank's Digital Merchant Commerce financial motion and settlement system. In the real DMC ecosystem, financial events flow through a Kafka-based pipeline spanning services such as `journalengine`, `journalproducer`, `journal-consumer`, `journalexceptions`, `settlement-processor`, and `merchantpayoutservice`. This portal sits at the customer-facing edge of that pipeline, exposing dispute management over the journal/settlement layer.

**Measurable objectives:**

| Objective | Success Metric |
|---|---|
| Customers can self-serve dispute submissions | Dispute form submission success rate ≥ 95% |
| Disputes are auto-classified on receipt | 100% of submitted disputes carry an AI-assigned category and priority within 5 seconds |
| Ops team resolution throughput | Disputes can be reviewed and resolved without leaving the portal |
| Customers receive clear resolution communication | Auto-generated plain-language summaries delivered on every resolved dispute |
| Runnable from a single command | `docker compose up` brings the full stack online in under 2 minutes |

### 1.2 Project Scope

#### In-Scope

- Customer authentication (login / session management via JWT)
- Transaction list view per customer with search and date filtering
- Dispute submission — both structured form and AI-assisted natural language entry
- AI-powered dispute classification (category + priority) on submission
- Dispute status tracking for customers (history view with timeline)
- Operations dashboard: list all disputes, filter by status/priority/category, assign, resolve
- Auto-generated resolution summary delivered to customer on closure
- Kafka event publishing for `dispute.submitted`, `dispute.classified`, `dispute.resolved`
- Dockerised deployment via Docker Compose
- Swagger/OpenAPI documentation for all backend endpoints
- Unit and integration tests for backend; component tests for frontend
- README with full build/run/test instructions

#### Out-of-Scope

- Real payment network integration (Visa/Mastercard chargebacks)
- Email/SMS notification delivery (notifications are simulated — stored in DB, surfaced in UI)
- Real Keycloak/IdP integration (JWT auth is self-contained, Keycloak-style but embedded)
- Mobile application
- Multi-currency support
- File/evidence upload for disputes
- SLA enforcement or escalation workflows

### 1.3 Target Audience / Personas

**Persona 1 — Bank Customer (Maya)**  
Maya is a Capitec retail customer who uses her card for daily purchases. She occasionally sees transactions she does not recognise or that appear duplicated. She wants a simple way to flag a problem without calling a branch, and to know what is happening with her dispute.

**Persona 2 — Operations Analyst (Sipho)**  
Sipho works in Capitec's Digital Merchant Commerce operations team. He receives a queue of disputes daily, needs to quickly understand priority and category, investigate by viewing transaction details, and formally close cases with a written resolution. He values tools that reduce manual classification work.

**Persona 3 — Operations Manager (Zanele)**  
Zanele oversees the DMC dispute team. She needs visibility into dispute volumes, resolution rates, and pending backlogs. She does not resolve individual disputes but monitors team performance.

### 1.4 Glossary

| Term | Definition |
|---|---|
| **DMC** | Digital Merchant Commerce — Capitec Bank's merchant payment and settlement domain |
| **Journal Entry** | A financial record of a transaction event, produced by the journalengine service |
| **Dispute** | A customer-initiated challenge against a specific transaction |
| **Dispute Category** | The classification of why a dispute was raised (e.g. unauthorised, duplicate) |
| **Priority** | AI-assigned urgency score: Low / Medium / High / Critical |
| **Resolution** | The formal closure of a dispute with an outcome (Upheld / Declined / Partial) |
| **Resolution Summary** | AI-generated plain-language explanation of the resolution, sent to the customer |
| **Kafka Topic** | A named stream of events in Apache Kafka |
| **NLP Extraction** | Using a large language model to parse free-text input into structured fields |
| **LLM** | Large Language Model — specifically the Anthropic Claude API in this project |
| **JWT** | JSON Web Token — the auth mechanism used for session management |
| **EF Core** | Entity Framework Core — the .NET ORM used for database access |
| **GitOps** | The practice of managing deployment configuration in Git (as used in DMC via ArgoCD) |

---

## 2. User & Business Requirements

### 2.1 User Stories & Use Cases

#### Authentication

| ID | User Story |
|---|---|
| AUTH-01 | As a customer, I want to log in with my account credentials so that I can access my transaction history and disputes. |
| AUTH-02 | As an ops analyst, I want to log in with an ops-role account so that I can access the operations dashboard. |
| AUTH-03 | As any user, I want my session to expire after inactivity so that my account is protected on shared devices. |

#### Transaction Viewing

| ID | User Story |
|---|---|
| TXN-01 | As a customer, I want to see a paginated list of my recent transactions so that I can review my account activity. |
| TXN-02 | As a customer, I want to filter transactions by date range and merchant name so that I can locate a specific transaction quickly. |
| TXN-03 | As a customer, I want to see transaction details (amount, merchant, date, reference, status) so that I have full context before raising a dispute. |

#### Dispute Submission

| ID | User Story |
|---|---|
| DISP-01 | As a customer, I want to raise a dispute against a transaction using a structured form so that I can provide the required details. |
| DISP-02 | As a customer, I want to describe my problem in plain language and have the system extract the relevant details automatically so that I do not need to fill in every field manually. |
| DISP-03 | As a customer, I want to confirm the AI-extracted fields before final submission so that I can correct any inaccuracies. |
| DISP-04 | As a customer, I want to receive a dispute reference number on submission so that I can track my case. |

#### Dispute Tracking

| ID | User Story |
|---|---|
| TRACK-01 | As a customer, I want to view all my disputes and their current statuses so that I know the state of each case. |
| TRACK-02 | As a customer, I want to see a timeline of events for each dispute (submitted, under review, resolved) so that I understand what has happened. |
| TRACK-03 | As a customer, I want to read the resolution summary when my dispute is closed so that I understand the outcome in plain language. |

#### Operations Dashboard

| ID | User Story |
|---|---|
| OPS-01 | As an ops analyst, I want to see all open disputes ranked by priority so that I work the most critical cases first. |
| OPS-02 | As an ops analyst, I want to filter disputes by category, priority, and status so that I can manage my queue efficiently. |
| OPS-03 | As an ops analyst, I want to view full dispute and transaction details on a single screen so that I can investigate without switching systems. |
| OPS-04 | As an ops analyst, I want to resolve a dispute with an outcome (Upheld / Declined / Partial) and internal notes so that the case is formally closed. |
| OPS-05 | As an ops analyst, I want the system to auto-generate a customer-facing resolution summary from my notes so that I do not have to write separate communications. |
| OPS-06 | As an ops manager, I want a summary view of dispute volumes, open counts, and average resolution time so that I can monitor team performance. |

#### AI Features

| ID | User Story |
|---|---|
| AI-01 | As a customer, I want to type a free-text description of my problem and have the AI fill in the dispute form for me so that submission is fast and low-friction. |
| AI-02 | As the system, I want every submitted dispute to be automatically classified by category and priority using AI so that ops analysts receive pre-triaged cases. |
| AI-03 | As an ops analyst, I want the system to generate a plain-language resolution summary for the customer from my internal resolution notes so that I save time on written communication. |

---

### 2.2 User Journeys / Flows

#### Journey 1 — Customer Raises a Dispute (Natural Language)

1. Customer logs in and lands on the Transactions page.
2. Customer scrolls to a suspicious transaction and clicks **"Dispute this transaction"**.
3. Customer is presented with two tabs: **Structured Form** and **Describe in your own words**.
4. Customer selects the natural language tab and types: *"I was charged R450 twice at Shoprite on 14 July but I only shopped once."*
5. System sends the text to the AI extraction endpoint. A loading indicator is shown.
6. System displays a pre-filled form: Reason = Duplicate Charge, Amount = R450, Merchant = Shoprite, Date = 14 July 2026.
7. Customer reviews, optionally edits, and clicks **Submit**.
8. System publishes a `dispute.submitted` Kafka event.
9. AI classification runs asynchronously; dispute record is updated with category and priority.
10. Customer sees a confirmation screen with their dispute reference number (e.g. `DSP-20260714-00042`).

#### Journey 2 — Ops Analyst Resolves a Dispute

1. Analyst logs in and lands on the Operations Dashboard.
2. Dashboard displays open disputes sorted by priority descending.
3. Analyst clicks a **Critical** priority dispute.
4. Detail view shows: customer info, original transaction, AI-assigned category/priority, customer's description, submission timeline.
5. Analyst reviews and clicks **Resolve**.
6. A resolution modal prompts for: Outcome (Upheld / Declined / Partial) and Internal Notes.
7. Analyst enters notes: *"Transaction confirmed as duplicate — refund initiated via settlement-processor."*
8. Analyst clicks **Generate Summary**. System calls AI endpoint and displays a preview of the customer-facing summary.
9. Analyst reviews summary, optionally edits, and clicks **Confirm Resolution**.
10. System updates dispute status to Resolved, stores summary, publishes `dispute.resolved` Kafka event.
11. Customer sees the resolution summary and status **Resolved** on their dispute detail page.

#### Journey 3 — Customer Views Dispute History

1. Customer logs in and navigates to **My Disputes**.
2. A list of all past and open disputes is shown with status badges (Open / Under Review / Resolved).
3. Customer clicks a resolved dispute.
4. Full timeline is displayed: Submitted → Under Review → Resolved, with timestamps.
5. Resolution summary is shown in a highlighted panel at the bottom.

---

### 2.3 Acceptance Criteria

#### AC-AUTH-01: Customer Login
- Given valid credentials, the system returns a JWT with a 60-minute expiry.
- Given invalid credentials, the system returns HTTP 401 with a generic error message (no credential enumeration).
- Given an expired JWT, all protected API calls return HTTP 401.

#### AC-TXN-01: Transaction List
- Transaction list returns paginated results (default 20 per page).
- Each record shows: transaction reference, merchant name, amount, currency, date, and status.
- Filtering by date range returns only transactions within the range, inclusive of boundary dates.

#### AC-DISP-02: Natural Language Extraction (AI-01)
- Given a plain-text description containing a merchant name, amount, and reason, the AI returns extracted fields within 5 seconds.
- Extracted fields are pre-populated in the structured form for customer review.
- If extraction confidence is low, the relevant field is left blank with a placeholder prompting the customer to fill it in.
- The customer can edit any extracted field before submitting.

#### AC-DISP-04: Dispute Submission Confirmation
- On successful submission, the customer receives a unique dispute reference in the format `DSP-YYYYMMDD-NNNNN`.
- A `dispute.submitted` Kafka event is published within 1 second of HTTP 201 being returned.

#### AC-AI-02: Intelligent Dispute Classification
- Every dispute is classified within 5 seconds of the `dispute.submitted` Kafka event.
- Category is one of: `UNAUTHORISED`, `DUPLICATE_CHARGE`, `MERCHANT_ERROR`, `WRONG_AMOUNT`, `OTHER`.
- Priority is one of: `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`.
- Classification result is stored on the dispute record and visible in the ops dashboard.
- If the AI call fails, the dispute is flagged as `CLASSIFICATION_FAILED` and surfaced for manual triage; the submission is not blocked.

#### AC-OPS-04 / AC-AI-03: Resolution & Auto-Summary
- The resolution modal accepts an outcome and free-text notes (minimum 20 characters).
- The AI-generated summary is a plain-language paragraph of 2–4 sentences explaining the outcome.
- The summary is displayed to the customer on their dispute detail page once the dispute is resolved.
- A `dispute.resolved` Kafka event is published on resolution.

#### AC-OPS-06: Ops Summary Dashboard
- Dashboard displays: total open disputes, count by priority, count by category, average resolution time (last 30 days).
- Data refreshes on page load; no real-time push required.

#### AC-NFR: Docker
- Running `docker compose up --build` from the repository root starts all services (api, frontend, postgres, kafka, zookeeper) without manual configuration.
- The frontend is accessible at `http://localhost:3000` and the API at `http://localhost:5000`.
- Swagger UI is accessible at `http://localhost:5000/swagger`.

---

## 3. Technical Specifications

### 3.1 System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Client Browser                           │
│                   React (TypeScript) SPA                        │
│                    shadcn/ui + TanStack Query                   │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTPS / REST (JSON)
┌──────────────────────────▼──────────────────────────────────────┐
│               C# .NET 8 Web API (ASP.NET Core)                  │
│  Controllers → Services → Repositories → EF Core               │
│  Serilog (structured JSON logs)  │  Swagger/OpenAPI             │
│  JWT Middleware                  │  Health endpoints            │
└────────┬─────────────────────────┬───────────────────────────────┘
         │ EF Core / Npgsql        │ Confluent.Kafka
┌────────▼──────────┐   ┌──────────▼──────────────────────────────┐
│   PostgreSQL 16   │   │         Apache Kafka                     │
│  (transactions,   │   │  Topics:                                 │
│   disputes,       │   │    dispute.submitted                     │
│   users,          │   │    dispute.classified                    │
│   resolutions)    │   │    dispute.resolved                      │
└───────────────────┘   └──────────────────────────────────────────┘
                                   │
         ┌─────────────────────────▼──────────────────────────────┐
         │            Dispute Classification Consumer              │
         │    (Hosted Service within the same .NET process)        │
         │    Subscribes to dispute.submitted                      │
         │    Calls Anthropic Claude API → publishes               │
         │    dispute.classified                                    │
         └────────────────────────────────────────────────────────┘
                                   │
         ┌─────────────────────────▼──────────────────────────────┐
         │              Anthropic Claude API                       │
         │    claude-haiku-4-5  (extraction, classification)       │
         │    claude-sonnet-5   (resolution summary generation)    │
         └────────────────────────────────────────────────────────┘
```

**Docker Compose services:**

```yaml
services:
  postgres:
    image: postgres:16-alpine
    ports: ["5432:5432"]
    environment:
      POSTGRES_DB: disputeportal
      POSTGRES_USER: dp_user
      POSTGRES_PASSWORD: dp_pass

  zookeeper:
    image: confluentinc/cp-zookeeper:7.6.0
    ports: ["2181:2181"]

  kafka:
    image: confluentinc/cp-kafka:7.6.0
    depends_on: [zookeeper]
    ports: ["9092:9092"]
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"

  api:
    build: ./src/DisputePortal.Api
    ports: ["5000:8080"]
    depends_on: [postgres, kafka]
    environment:
      ConnectionStrings__Default: "Host=postgres;Database=disputeportal;Username=dp_user;Password=dp_pass"
      Kafka__BootstrapServers: "kafka:9092"
      Anthropic__ApiKey: "${ANTHROPIC_API_KEY}"
      Jwt__Secret: "${JWT_SECRET}"

  frontend:
    build: ./src/dispute-portal-ui
    ports: ["3000:80"]
    depends_on: [api]
```

---

### 3.2 Data Models & Database Design

#### Entity: User

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `email` | VARCHAR(255) UNIQUE | |
| `password_hash` | TEXT | bcrypt |
| `full_name` | VARCHAR(255) | |
| `role` | VARCHAR(50) | `CUSTOMER`, `OPS_ANALYST`, `OPS_MANAGER` |
| `created_at` | TIMESTAMPTZ | |

#### Entity: Transaction

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `customer_id` | UUID FK → User | |
| `reference` | VARCHAR(100) UNIQUE | e.g. `TXN-20260714-00001` |
| `merchant_name` | VARCHAR(255) | |
| `merchant_category` | VARCHAR(100) | MCC description |
| `amount` | DECIMAL(18,2) | |
| `currency` | CHAR(3) | ISO 4217, default `ZAR` |
| `transaction_date` | TIMESTAMPTZ | |
| `status` | VARCHAR(50) | `SETTLED`, `PENDING`, `REVERSED` |
| `created_at` | TIMESTAMPTZ | |

#### Entity: Dispute

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `reference` | VARCHAR(30) UNIQUE | `DSP-YYYYMMDD-NNNNN` |
| `transaction_id` | UUID FK → Transaction | |
| `customer_id` | UUID FK → User | |
| `status` | VARCHAR(50) | `OPEN`, `UNDER_REVIEW`, `RESOLVED`, `CLASSIFICATION_FAILED` |
| `category` | VARCHAR(50) | `UNAUTHORISED`, `DUPLICATE_CHARGE`, `MERCHANT_ERROR`, `WRONG_AMOUNT`, `OTHER`, NULL until classified |
| `priority` | VARCHAR(20) | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`, NULL until classified |
| `customer_description` | TEXT | Raw text from customer |
| `extracted_fields_json` | JSONB | AI-extracted fields before confirmation |
| `assigned_to_id` | UUID FK → User NULL | Ops analyst |
| `created_at` | TIMESTAMPTZ | |
| `updated_at` | TIMESTAMPTZ | |

#### Entity: DisputeEvent

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `dispute_id` | UUID FK → Dispute | |
| `event_type` | VARCHAR(100) | `SUBMITTED`, `CLASSIFIED`, `ASSIGNED`, `UNDER_REVIEW`, `RESOLVED` |
| `actor_id` | UUID FK → User NULL | NULL for system events |
| `description` | TEXT | Human-readable event description |
| `created_at` | TIMESTAMPTZ | |

#### Entity: Resolution

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `dispute_id` | UUID FK → Dispute UNIQUE | |
| `outcome` | VARCHAR(50) | `UPHELD`, `DECLINED`, `PARTIAL` |
| `internal_notes` | TEXT | Written by ops analyst |
| `customer_summary` | TEXT | AI-generated plain-language summary |
| `resolved_by_id` | UUID FK → User | |
| `resolved_at` | TIMESTAMPTZ | |

**Relationships summary:**
- One User → many Transactions (customer)
- One Transaction → zero or one Dispute
- One Dispute → many DisputeEvents
- One Dispute → zero or one Resolution

---

### 3.3 API & Integration Specs

All endpoints are prefixed `/api/v1`. All requests and responses are `application/json`. Authentication is required on all endpoints unless marked **[Public]**.

#### Auth Endpoints

| Method | Path | Description | Request Body | Response |
|---|---|---|---|---|
| POST | `/auth/login` **[Public]** | Authenticate and receive JWT | `{ email, password }` | `{ token, expiresAt, user: { id, fullName, role } }` |
| POST | `/auth/logout` | Invalidate session (client-side token discard) | — | `204` |

#### Transaction Endpoints

| Method | Path | Description | Query Params | Response |
|---|---|---|---|---|
| GET | `/transactions` | List caller's transactions | `page`, `pageSize`, `from`, `to`, `merchant` | `{ items: Transaction[], total, page, pageSize }` |
| GET | `/transactions/{id}` | Get single transaction | — | `Transaction` |

#### Dispute Endpoints

| Method | Path | Description | Request Body | Response |
|---|---|---|---|---|
| POST | `/disputes` | Submit a dispute | `{ transactionId, category?, description, extractedFields? }` | `201 { id, reference, status }` |
| GET | `/disputes` | List caller's disputes (customer) or all disputes (ops) | `page`, `pageSize`, `status`, `priority`, `category` | `{ items: Dispute[], total, page, pageSize }` |
| GET | `/disputes/{id}` | Get dispute detail with event timeline | — | `DisputeDetail` |
| PATCH | `/disputes/{id}/status` | Update status (ops only) | `{ status }` | `200 Dispute` |
| POST | `/disputes/{id}/resolve` | Resolve a dispute (ops only) | `{ outcome, internalNotes, customerSummary }` | `200 Resolution` |

#### AI Endpoints

| Method | Path | Description | Request Body | Response |
|---|---|---|---|---|
| POST | `/ai/extract-dispute` | Extract structured fields from free text | `{ text }` | `{ transactionRef?, category?, amount?, merchantName?, transactionDate?, confidence: { [field]: number } }` |
| POST | `/ai/generate-summary` | Generate customer resolution summary (ops only) | `{ disputeId, outcome, internalNotes }` | `{ summary: string }` |

#### Dashboard Endpoints (Ops Manager / Analyst)

| Method | Path | Description | Response |
|---|---|---|---|
| GET | `/dashboard/summary` | Aggregated dispute stats | `{ totalOpen, byPriority, byCategory, avgResolutionHours }` |

---

### 3.4 Kafka Event Schemas

#### Topic: `dispute.submitted`

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

#### Topic: `dispute.classified`

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

#### Topic: `dispute.resolved`

```json
{
  "eventId": "uuid",
  "occurredAt": "ISO8601",
  "disputeId": "uuid",
  "reference": "string",
  "outcome": "UPHELD",
  "resolvedById": "uuid",
  "customerSummaryProvided": true
}
```

---

### 3.5 AI Integration Details

#### Anthropic SDK

The backend uses the `Anthropic` NuGet package (or raw `HttpClient` against `https://api.anthropic.com/v1/messages`). The API key is injected via environment variable `ANTHROPIC_API_KEY`.

#### Feature 1: Natural Language Extraction

**Model:** `claude-haiku-4-5-20251001` (low latency, cost-efficient)

**System prompt pattern:**
```
You are a dispute intake assistant for a bank. Extract structured dispute fields from the customer's description.
Return a JSON object with these optional fields: transactionRef, category (one of UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER), amount (number), merchantName, transactionDate (ISO8601 date), and a confidence map (0.0–1.0 per field).
If a field cannot be determined, omit it. Return only valid JSON.
```

**User message:** The raw customer text.

**Response handling:** Parse the JSON response; fields with confidence < 0.6 are highlighted in the UI for customer review.

#### Feature 2: Intelligent Dispute Classification

**Model:** `claude-haiku-4-5-20251001`  
**Trigger:** Background hosted service consumes `dispute.submitted` topic.

**System prompt pattern:**
```
You are a financial dispute triage engine. Classify the following dispute.
Return a JSON object: { "category": "<CATEGORY>", "priority": "<PRIORITY>", "rationale": "<one sentence>" }
Category must be one of: UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER.
Priority must be one of: LOW, MEDIUM, HIGH, CRITICAL.
Base priority on: amount (>R5000 = HIGH baseline), category (UNAUTHORISED = bump one level), and any prior open disputes by this customer.
Return only valid JSON.
```

**Context injected into user message:**
```
Transaction: { merchant, amount, date, merchantCategory }
Customer description: "<text>"
Customer open dispute count: <n>
```

#### Feature 3: Auto-Generated Resolution Summary

**Model:** `claude-sonnet-5` (higher quality for customer-facing text)

**System prompt pattern:**
```
You are a customer communication specialist at a bank. Write a clear, empathetic, plain-language summary (2–4 sentences) for the customer explaining the outcome of their transaction dispute.
Do not use jargon. Do not reveal internal investigation details. Be specific about the outcome.
Return only the summary text, no JSON wrapper.
```

**User message:**
```
Dispute reference: DSP-20260714-00042
Transaction: R450.00 at Shoprite on 14 July 2026
Outcome: UPHELD
Internal notes: "Transaction confirmed as duplicate — refund of R450 initiated."
```

---

### 3.6 Non-Functional Requirements

| Category | Requirement | Target |
|---|---|---|
| **Performance** | API P95 response time (non-AI endpoints) | < 300ms |
| **Performance** | AI extraction endpoint response time | < 5 seconds |
| **Performance** | AI classification (async, background) | < 5 seconds after Kafka event consumed |
| **Security** | All API endpoints require valid JWT (except `/auth/login`) | Enforced via ASP.NET Core middleware |
| **Security** | Passwords stored as bcrypt hashes (work factor ≥ 12) | Enforced at registration/seed |
| **Security** | API key never exposed to frontend | `ANTHROPIC_API_KEY` accessed server-side only |
| **Security** | SQL injection prevention | EF Core parameterised queries throughout |
| **Security** | CORS restricted to known frontend origin | Configured in `Program.cs` |
| **Scalability** | Kafka consumer group allows horizontal scaling | `GroupId` set; multiple instances safe |
| **Reliability** | AI classification failure does not block dispute submission | Dispute status falls back to `CLASSIFICATION_FAILED` |
| **Reliability** | DB migrations run automatically on API startup | EF Core `MigrateAsync()` in startup |
| **Observability** | All requests logged with correlation ID, status code, duration | Serilog request logging middleware |
| **Observability** | Kafka publish/consume events logged with topic, partition, offset | Confluent.Kafka callbacks |
| **Accessibility** | Frontend meets WCAG 2.1 AA for colour contrast, keyboard nav, ARIA labels | shadcn/ui components are accessible by default |
| **Portability** | Full stack runs via `docker compose up --build` | Single command, no pre-requisites beyond Docker |
| **Documentation** | All API endpoints documented via Swagger | Swagger UI enabled in Development and Docker environments |

---

## 4. Project Management & Delivery

### 4.1 Milestones & Timeline

Today is 16 July 2026. Deadline is 23 July 2026 at 23:59 (7 days).

| Day | Date | Milestone | Deliverables |
|---|---|---|---|
| 1 | 16 Jul | **Foundation** | Repo structure, Docker Compose scaffold, EF Core models, DB migrations, JWT auth, seed data |
| 2 | 17 Jul | **Transaction & Dispute APIs** | Full CRUD for transactions and disputes, Kafka producer wired, Swagger docs |
| 3 | 18 Jul | **AI Integration** | NLP extraction endpoint, classification background service (Kafka consumer), resolution summary endpoint |
| 4 | 19 Jul | **Frontend — Auth & Transactions** | Login page, transaction list with filter, transaction detail |
| 5 | 20 Jul | **Frontend — Disputes** | Dispute submission (structured + NL tab), dispute history, dispute detail with timeline |
| 6 | 21 Jul | **Frontend — Ops Dashboard** | Ops queue, resolve modal, auto-summary preview, dashboard metrics |
| 7 | 22 Jul | **Polish, Tests & README** | Unit + integration tests, component tests, Dockerfile finalised, README written, end-to-end smoke test |
| 7+| 23 Jul | **Buffer & Submission** | Final review, public GitHub repo confirmed, link submitted |

### 4.2 Resource Allocation

| Resource | Detail |
|---|---|
| Developer | Bongani Duma (sole contributor) |
| AI API | Anthropic Claude API — `claude-haiku-4-5-20251001` and `claude-sonnet-5` |
| Hosting | Local Docker Compose (no cloud hosting required for submission) |
| Source control | GitHub (public repository) |
| IDE | Visual Studio / Rider (backend), VS Code (frontend) |
| Time budget | ~6–8 hours/day × 7 days ≈ 50 hours |

### 4.3 Risk Assessment & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Anthropic API rate limits or latency | Low | High | Cache classification results; AI features degrade gracefully (not blocking); use Haiku for extraction and classification |
| Kafka setup complexity in Docker | Medium | Medium | Use `confluentinc/cp-kafka` with `KAFKA_AUTO_CREATE_TOPICS_ENABLE=true`; topics created at API startup if missing |
| EF Core migration issues on fresh DB | Low | Medium | Run `MigrateAsync()` on startup; include migration scripts in repo |
| Time overrun on frontend | Medium | High | Build ops dashboard last; core customer flows (submit, view, track) are mandatory; dashboard is nice-to-have |
| JWT secret misconfiguration | Low | High | Strong default in `docker-compose.override.yml`; documented in README with mandatory override warning |
| AI extraction produces incorrect fields | Medium | Low | Confidence threshold UI; customer reviews extracted fields before submitting |

### 4.4 Testing Strategy

#### Backend (xUnit + ASP.NET Core TestServer)

**Unit tests** cover:
- `DisputeService.SubmitDisputeAsync` — happy path and duplicate dispute guard
- `AiClassificationService.ClassifyAsync` — mock Anthropic HTTP client, verify category/priority mapping
- `DisputeReferenceGenerator.Generate` — format validation
- JWT middleware — valid token, expired token, missing token

**Integration tests** use `WebApplicationFactory<Program>` with a real PostgreSQL test container (Testcontainers library):
- `POST /api/v1/disputes` → assert 201, dispute reference format, Kafka message published
- `POST /api/v1/disputes/{id}/resolve` → assert resolution persisted, Kafka event published
- `POST /api/v1/ai/extract-dispute` → assert response shape (mock Anthropic in integration env)

#### Frontend (Vitest + React Testing Library)

**Component tests** cover:
- `DisputeForm` — renders structured and NL tabs; NL tab calls extract endpoint on button click; form submission disabled until required fields populated
- `DisputeList` — renders dispute rows with correct status badges
- `DisputeTimeline` — renders events in chronological order
- `OpsResolveModal` — outcome dropdown required; generate summary button calls AI endpoint; confirm button disabled until summary present

#### Manual QA checklist (pre-submission)
- [ ] `docker compose up --build` completes without errors on a clean machine
- [ ] Seed customer and ops accounts work
- [ ] Full customer journey: login → view transactions → submit NL dispute → view dispute reference
- [ ] Full ops journey: login → open ops queue → resolve dispute → view auto-generated summary
- [ ] Swagger UI lists all endpoints and shows correct schemas
- [ ] `docker compose down -v && docker compose up --build` (fresh state) works identically

#### CI (optional, if time permits)
- GitHub Actions workflow: `dotnet test` + `npm run test` on push to `main`

---

*End of specification.*
