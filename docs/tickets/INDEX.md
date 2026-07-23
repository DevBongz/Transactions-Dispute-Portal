# Transactions Dispute Portal — Development Tickets

This directory contains the full set of development tickets for the **Transactions Dispute Portal**
(Option 2 — DMC Fin-Motion). Tickets follow a consistent Jira-style format and are grouped by the
delivery milestones defined in [`SPEC.md`](../../SPEC.md) §4.1.

**Project prefix:** `TDP-` (Transactions Dispute Portal)

## Ticket groups

### Group A — Foundation (Day 1, 16 Jul)
| ID | Title | Depends on |
|---|---|---|
| [TDP-INFRA-01](TDP-INFRA-01.md) | Repository Structure & Solution Scaffold | — |
| [TDP-INFRA-02](TDP-INFRA-02.md) | Docker Compose Stack | TDP-INFRA-01 |
| [TDP-DATA-01](TDP-DATA-01.md) | EF Core Domain Models & DbContext | TDP-INFRA-01 |
| [TDP-DATA-02](TDP-DATA-02.md) | Database Migrations & Seed Data | TDP-DATA-01 |
| [TDP-AUTH-01](TDP-AUTH-01.md) | JWT Authentication & Role-Based Authorization | TDP-DATA-01, TDP-DATA-02 |

### Group B — Backend APIs (Day 2, 17 Jul)
| ID | Title | Depends on |
|---|---|---|
| [TDP-KAFKA-01](TDP-KAFKA-01.md) | Kafka Producer & Domain Event Publishing | TDP-INFRA-02 |
| [TDP-TXN-01](TDP-TXN-01.md) | Transaction Listing & Detail API | TDP-DATA-02, TDP-AUTH-01 |
| [TDP-DISP-01](TDP-DISP-01.md) | Dispute Submission API & Reference Generator | TDP-DATA-02, TDP-AUTH-01, TDP-KAFKA-01 |
| [TDP-DISP-02](TDP-DISP-02.md) | Dispute Listing, Detail & Status Update API | TDP-DISP-01 |
| [TDP-DISP-03](TDP-DISP-03.md) | Dispute Resolution API | TDP-DISP-01, TDP-KAFKA-01 |
| [TDP-OBS-01](TDP-OBS-01.md) | Serilog Logging, Correlation IDs & Health Endpoints | TDP-INFRA-01 |

### Group C — AI Integration (Day 3, 18 Jul)
| ID | Title | Depends on |
|---|---|---|
| [TDP-AI-01](TDP-AI-01.md) | Natural Language Dispute Extraction Endpoint | TDP-AUTH-01 |
| [TDP-AI-02](TDP-AI-02.md) | Dispute Classification Background Consumer | TDP-KAFKA-01, TDP-DISP-01 |
| [TDP-AI-03](TDP-AI-03.md) | Resolution Summary Generation Endpoint | TDP-DISP-03 |

### Group D — Frontend (Days 4–6, 19–21 Jul)
| ID | Title | Depends on |
|---|---|---|
| [TDP-FE-01](TDP-FE-01.md) | Frontend Scaffold, Routing, Auth Context & API Client | TDP-AUTH-01 |
| [TDP-FE-02](TDP-FE-02.md) | Transaction List & Detail Views | TDP-FE-01, TDP-TXN-01 |
| [TDP-FE-03](TDP-FE-03.md) | Dispute Submission UI (Structured + NL) | TDP-FE-01, TDP-DISP-01, TDP-AI-01 |
| [TDP-FE-04](TDP-FE-04.md) | Customer Dispute History & Timeline Views | TDP-FE-01, TDP-DISP-02 |
| [TDP-FE-05](TDP-FE-05.md) | Operations Dashboard, Resolve Modal & Metrics | TDP-FE-01, TDP-DISP-02, TDP-DISP-03, TDP-AI-03 |

### Group E — Quality & Delivery (Day 7, 22–23 Jul)
| ID | Title | Depends on |
|---|---|---|
| [TDP-TEST-01](TDP-TEST-01.md) | Backend Unit & Integration Tests | TDP-TXN-01, TDP-DISP-01, TDP-DISP-03, TDP-AI-02 |
| [TDP-TEST-02](TDP-TEST-02.md) | Frontend Component Tests | TDP-FE-03, TDP-FE-04, TDP-FE-05 |
| [TDP-DOC-01](TDP-DOC-01.md) | Swagger/OpenAPI Documentation | Backend API tickets |
| [TDP-DOC-02](TDP-DOC-02.md) | README & Operational Runbook | TDP-INFRA-02 + all feature tickets |
| [TDP-CICD-01](TDP-CICD-01.md) | GitHub Actions CI Pipeline | TDP-TEST-01, TDP-TEST-02 |

## Suggested delivery order

The dependency graph collapses to this critical path:

```
INFRA-01 → INFRA-02 → KAFKA-01 ┐
        ↘ DATA-01 → DATA-02 → AUTH-01 → {TXN-01, DISP-01} → DISP-02 → DISP-03
                                          DISP-01 ↘ AI-02
                                          AI-01, AI-03
        AUTH-01 → FE-01 → {FE-02, FE-03, FE-04, FE-05}
        (all features) → {TEST-01, TEST-02, DOC-01, DOC-02} → CICD-01
```

OBS-01 can proceed in parallel once INFRA-01 lands. AI and Frontend groups can be developed
concurrently once their respective backend dependencies are in place.

## ⚠️ Open questions / flags to resolve

These were raised during ticket authoring and need a decision before the affected tickets are picked up.
Intended to be sorted out separately (e.g. with another LLM or the team).

| # | Ticket(s) | Flag | Options |
|---|---|---|---|
| 1 | [TDP-INFRA-02](TDP-INFRA-02.md) | **Kafka listener config deviates from SPEC §3.1.** The spec's single `kafka:9092` listener does not work for both in-container and host access. The ticket uses dual listeners (`kafka:29092` internal / `localhost:9092` host). | (a) Keep the dual-listener deviation (recommended — it works). (b) Revert to the SPEC value and accept host-access limitations. Confirm and update SPEC §3.1 to match. |
| 2 | [TDP-AI-01](TDP-AI-01.md), [TDP-FE-03](TDP-FE-03.md) | **Low-confidence AI field handling contradicts AC-DISP-02.** SPEC §2.3 (AC-DISP-02) says a field with confidence < 0.6 should be left **blank**. FE-03 instead shows the AI's guess **and flags it** for review. | (a) Show-and-flag (current FE-03 behaviour — better UX). (b) Strict blanking per AC-DISP-02 (FE-03 notes a one-line toggle for this). Pick one and align the ticket + SPEC. |
| 3 | [TDP-FE-04](TDP-FE-04.md) | **`CLASSIFICATION_FAILED` customer-facing display.** FE-04 maps the internal `CLASSIFICATION_FAILED` status to "Under Review" in the customer view only (ops still see the real status). Confirm this is the desired customer-facing behaviour. | (a) Show "Under Review" to customers (current). (b) Show a distinct customer-facing state. |
| 4 | [TDP-TEST-01](TDP-TEST-01.md), [TDP-DOC-02](TDP-DOC-02.md) | **Placeholder names to reconcile.** Test/doc tickets reference expected class/DTO/exception names (e.g. `SubmitDisputeCommand`, `DisputeCreatedResponse`, `IEventPublisher`) and seed credentials that don't exist yet — the source is only SPEC.md + scaffold. Reconcile once TDP-DISP-01 / TDP-DATA-02 are implemented. | Update the test/doc tickets to match the actual implemented names. |

### Resolutions

- **Flag 1 (Kafka dual listeners):** resolved as **(a)** — keep dual listeners (`PLAINTEXT://kafka:29092` in-container, `PLAINTEXT_HOST://localhost:9092` on the host). Documented in the root README troubleshooting section. SPEC §3.1 remains the conceptual single-broker view; Compose implements the dual-listener practicality.
- **Flag 2 (low-confidence AI fields):** resolved as **(a) show-and-flag** during the Batch 6
  frontend build. FE-03 shows the AI's best guess with an amber ring + "please confirm" helper
  text and `aria-describedby`; editing a flagged field clears the flag. The `CONFIDENCE_THRESHOLD`
  (0.6) is centralised in `src/features/disputes/types.ts`, so switching to strict blanking is a
  one-line change if the SPEC owner later prefers option (b).
- **Flag 3 (`CLASSIFICATION_FAILED` customer display):** resolved as **(a)** — customers see
  "Under Review"; ops see the raw "Needs Triage" state. Implemented in `StatusBadge` via the
  `customerView` prop.
- **Flag 4 (placeholder names):** resolved — implementation uses `SubmitDisputeRequest` /
  `SubmitDisputeResponse`, `IEventPublisher`, `IAnthropicClient` (Gemini-backed), seed emails
  `maya@example.com` / `sipho@capitec.ops` / `zanele@capitec.ops`. README and integration tests
  match those names.

## Ticket format

Every ticket contains: **Jira summary**, then five sections — (1) Context & Motivation,
(2) Detailed Description, (3) Acceptance Criteria, (4) Technical Notes, (5) Definition of Done.
