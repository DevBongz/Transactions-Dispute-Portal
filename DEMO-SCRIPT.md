# Demo Script — Transactions Dispute Portal

**For:** Interview / project walkthrough  
**App URL:** https://dp-ui-5rd4.onrender.com  
**Suggested length:** 8–12 minutes  
**Password for all accounts:** `Password123!`

| Persona | Email | Role |
|---|---|---|
| Maya | `maya@example.com` | Customer |
| Sipho | `sipho@capitec.ops` | Ops analyst |
| Zanele | `zanele@capitec.ops` | Ops manager |

---

## Opening (30–45 seconds)

**What to say:**

> “This is the **Transactions Dispute Portal** — a full-stack app I built for Capitec’s DMC (Digital Merchant Commerce) context.
>
> In plain terms: customers can find a card transaction, raise a dispute, and track it. Operations staff triage and resolve those disputes. AI helps extract details from natural language, auto-classify priority/category, and draft customer-facing resolution summaries.
>
> Under the hood it’s a .NET 8 API, React SPA, Postgres, and Kafka (Redpanda) for async classification events — deployed on Render.”

**Optional one-liner architecture:**

> “Customer submit → event on Kafka → background consumer classifies → ops queue updates. Resolution publishes another event and the customer sees a plain-language summary.”

---

## Act 1 — Customer journey (Maya) · ~4 minutes

### 1.1 Login

1. Open https://dp-ui-5rd4.onrender.com/login  
2. Sign in as **Maya** (`maya@example.com` / `Password123!`)

**What to say:**

> “Authentication is JWT-based with role-based access. Maya is a retail customer — she only sees her own transactions and disputes.”

### 1.2 Transactions list

1. You should land on **Transactions**
2. Point out: amount, merchant, date, status, pagination
3. Optionally use filters (date / merchant) if visible

**What to say:**

> “This is her recent activity. Before disputing, she needs enough context — merchant, amount, date, reference, settlement status.”

### 1.3 Open a good demo transaction

Pick a clear demo row, preferably:

- **Shoprite R450** (duplicate-charge story), or  
- **Unknown Merch** (unauthorised story)

Click into the transaction detail.

**What to say:**

> “I’ll use this Shoprite charge — classic duplicate-charge scenario.”

### 1.4 Raise a dispute (AI natural language)

1. Click **Dispute this transaction** (or equivalent CTA)
2. Switch to the **natural language** tab (not only the structured form)
3. Paste something like:

> I was charged R450 twice at Shoprite on 14 July but I only shopped once. Please refund the duplicate.

4. Run extraction / “Analyse” (whatever the UI labels)
5. Show the filled fields + confidence (if shown)
6. Correct anything if needed, then **Submit**
7. Note the **dispute reference** on the confirmation screen

**What to say:**

> “Customers don’t always know category codes. They describe the problem in plain language. The AI extracts structured fields — category, amount, merchant, date — and Maya confirms before submit. That human-in-the-loop step matters: AI assists, it doesn’t auto-file blindly.
>
> On submit she gets a reference number. Behind the scenes we publish a `dispute.submitted` Kafka event. A background consumer calls the LLM to assign category and priority, then publishes `dispute.classified`.”

### 1.5 Track the dispute

1. Go to **My disputes** (or history)
2. Open the new dispute
3. Show status + **timeline** of events

**What to say:**

> “Maya can track status without calling a branch. The timeline shows submitted → classified / under review → later resolved. If classification fails, we degrade gracefully — the dispute still exists for manual ops triage.”

**Pause:** Log out Maya.

---

## Act 2 — Ops analyst (Sipho) · ~4 minutes

### 2.1 Login as Sipho

1. Log in: `sipho@capitec.ops` / `Password123!`
2. Land on the **ops dashboard / queue**

**What to say:**

> “Sipho is an ops analyst. Different role, different surface — priority queue, not personal transactions.”

### 2.2 Queue & triage

1. Point at open disputes ordered / filterable by **priority**, **category**, **status**
2. Find Maya’s dispute (by reference or merchant)
3. Open it

**What to say:**

> “The queue is built so critical cases surface first. AI classification reduces the manual ‘what kind of dispute is this?’ step so analysts spend time investigating, not labelling.”

### 2.3 Investigate

On the detail screen, walk through:

- Customer description  
- Transaction context  
- AI category / priority  
- Timeline  

**What to say:**

> “Everything needed to investigate is on one screen — dispute + transaction — so he doesn’t context-switch across systems.”

### 2.4 Resolve with AI summary

1. Open **Resolve**
2. Choose an outcome: e.g. **Upheld** (duplicate → refund)
3. Enter internal notes (≥ 20 characters), e.g.:

> Confirmed duplicate Shoprite settlement on 14 July. Second charge reversed; refund initiated to customer account.

4. Click **Generate summary**
5. Read the customer-facing text aloud briefly
6. Confirm / submit resolution

**What to say:**

> “Internal notes stay operational. The AI turns them into a plain-language customer summary so Sipho doesn’t write two versions of the same decision. On confirm we persist the resolution and publish `dispute.resolved`.”

**Pause:** Log out Sipho.

---

## Act 3 — Customer sees the outcome (Maya) · ~1 minute

1. Log back in as **Maya**
2. Open the same dispute
3. Show **Resolved** status + resolution summary + updated timeline

**What to say:**

> “Closing the loop: Maya sees the outcome in language she can understand — not an internal ops code.”

---

## Act 4 — Ops manager (Zanele) · ~1–2 minutes (optional but strong)

1. Log in: `zanele@capitec.ops` / `Password123!`
2. Show **dashboard metrics**: open volume, by priority, by category, average resolution time

**What to say:**

> “Zanele doesn’t resolve individual cases — she monitors backlog and throughput. Same platform, manager lens.”

---

## Closing — technical highlights (1–2 minutes)

Pick **3–4** points (don’t dump the whole stack unless asked):

| Point | One sentence |
|---|---|
| Roles | JWT + role policies: customer / analyst / manager see different routes and APIs. |
| Async AI | Classification is event-driven via Kafka so submit stays fast and failures don’t block filing. |
| AI assist | NL extraction + classification + resolution summary; keys stay server-side only. |
| Production | Deployed on Render with managed Postgres, Redpanda private service, Docker API, static SPA. |
| Observability | Health checks for Postgres + Kafka; structured logs with correlation IDs. |
| Runnable locally | `docker compose up --build` for full stack; Swagger at `/swagger`. |

**Closing line:**

> “Happy to go deeper on any layer — the dispute domain model, the Kafka consumer, the Gemini integration, or how we debugged the Render deployment.”

---

## If something goes wrong live

| Problem | What to do / say |
|---|---|
| Cold start / slow first load | “Starter instances may wake up — give it ~30s.” Refresh. |
| AI extraction / summary 502 | “LLM rate limit or timeout — structured form / manual notes still work; AI is assistive.” |
| Category still “Pending” | “Classifier may still be consuming — show timeline; can still resolve manually.” |
| Login fails | Confirm you’re on https://dp-ui-5rd4.onrender.com and using the table credentials. |
| Need proof API is up | Open https://dp-api-eebu.onrender.com/health/ready (should be Healthy). |

---

## Backup demo text snippets (copy-paste)

**NL dispute (duplicate):**
```text
I was charged R450 twice at Shoprite but I only made one purchase. Please investigate and refund the duplicate charge.
```

**NL dispute (unauthorised):**
```text
I don’t recognise a R7999.99 charge at Unknown Merch. I did not authorise this transaction.
```

**Internal resolve notes:**
```text
Verified against settlement journal: duplicate presentment confirmed. Refund of R450 initiated. Advising customer of 3–5 day clearing time.
```

---

## One-page “what this app does” (if they ask for a summary)

> Customers view card transactions and raise disputes (structured form or natural language). AI extracts fields and auto-classifies category/priority asynchronously over Kafka. Ops analysts work a priority queue, investigate, and resolve with outcomes; AI drafts the customer-facing resolution summary. Managers see volume and resolution metrics. The system is a .NET + React + Postgres + Kafka stack, deployed for this interview on Render.

---

*Tip: Before the interview, do a dry run once (Maya → Sipho → Maya → Zanele) so a classified dispute and metrics are warm in the demo environment.*
