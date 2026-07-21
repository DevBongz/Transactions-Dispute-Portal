# TDP-FE-03 — Dispute Submission UI (Structured + Natural Language tabs)

**Jira summary:** Build the dispute submission experience: a two-tab interface offering a **Structured Form** and a **"Describe in your own words"** natural-language entry. The NL tab sends free text to `POST /api/v1/ai/extract-dispute`, shows a loading state, then pre-fills the structured form with the extracted fields — highlighting low-confidence fields for the customer to review and edit — before submitting to `POST /api/v1/disputes` and landing on a confirmation screen showing the dispute reference. Implements Journey 1 and user stories DISP-01, DISP-02, DISP-03, DISP-04, AI-01 (AC-DISP-02, AC-DISP-04).

## 1. Context & Motivation

- **Background:** Reached via the "Dispute this transaction" CTA from TDP-FE-02 (`/disputes/new?transactionId={id}`). The backend provides AI extraction (`POST /ai/extract-dispute`, returns optional fields plus a `confidence` map, SPEC.md §3.3/§3.5) and dispute creation (`POST /disputes` → `201 { id, reference, status }`, which publishes `dispute.submitted`, SPEC.md §3.3/§3.4). Journey 1 (SPEC.md §2.2) defines the exact flow: two tabs, NL extraction with a loading indicator, a pre-filled reviewable form, submit, and a confirmation screen with a reference like `DSP-20260714-00042`.
- **Business Impact:** This is the portal's headline feature and a primary objective (dispute submission success rate ≥ 95%, SPEC.md §1.1). The NL path removes form friction (AI-01) while the confidence-highlight review step (AC-DISP-02) guards against incorrect AI extraction (a named project risk, SPEC.md §4.3) by keeping the human in the loop. The reference number (DISP-04) gives the customer something to track.
- **User Story:** As a customer (Maya), I want to either fill in a structured dispute form or simply describe my problem in plain language and have the system extract the details for me to confirm, so that raising a dispute is fast and low-friction and I still control what is submitted (DISP-01, DISP-02, DISP-03, DISP-04, AI-01).
- **Dependencies:** Depends on **TDP-FE-01** (routing, API client, types), **TDP-DISP-01** (`POST /disputes`, reference generator, `dispute.submitted`), and **TDP-AI-01** (`POST /ai/extract-dispute`). Entered from **TDP-FE-02**. On success, links into **TDP-FE-04** (My Disputes). Milestone: **Day 5 (20 Jul) — Frontend — Disputes**.

## 2. Detailed Description

### 2.1 Route & page shell

Route `/disputes/new` (customer-only, from TDP-FE-01). Reads `transactionId` from the query string; if absent or the transaction cannot be loaded, shows an error prompting the user to start from a transaction. Loads the bound transaction (reusing `useTransaction` from TDP-FE-02) and renders a summary header so the customer sees what they're disputing.

```tsx
// src/features/disputes/DisputeSubmitPage.tsx (structure)
export function Component() {
  const [sp] = useSearchParams();
  const transactionId = sp.get("transactionId") ?? "";
  const { data: txn, isLoading } = useTransaction(transactionId);
  const [step, setStep] = useState<"entry" | "confirmed">("entry");
  const [reference, setReference] = useState<string>();
  // header: TransactionSummaryCard(txn)
  // step === "entry"    -> <DisputeEntry transaction={txn} onSubmitted={(ref)=>{setReference(ref); setStep("confirmed");}} />
  // step === "confirmed"-> <DisputeConfirmation reference={reference!} />
}
```

### 2.2 Domain types & form schema

```ts
// src/features/disputes/types.ts
import type { DisputeCategory } from "@/types/api";

export interface ExtractDisputeResponse {
  transactionRef?: string;
  category?: DisputeCategory;
  amount?: number;
  merchantName?: string;
  transactionDate?: string;                 // ISO 8601 date
  confidence: Partial<Record<
    "transactionRef" | "category" | "amount" | "merchantName" | "transactionDate", number>>;
}

export interface DisputeFormValues {
  category: DisputeCategory | "";
  amount: string;
  merchantName: string;
  transactionDate: string;                  // yyyy-MM-dd
  description: string;                       // the customer's account of the problem
}
```

Validation (zod + `react-hook-form` via shadcn `Form`): `category` required (one of the five enum values), `description` required (min length so classification has signal), `amount` a positive number, `transactionDate` a valid date. Submit is disabled until required fields are valid.

### 2.3 Two-tab entry component

```tsx
// src/features/disputes/DisputeEntry.tsx (structure)
<Tabs defaultValue="nl">
  <TabsList aria-label="Dispute entry method">
    <TabsTrigger value="nl">Describe in your own words</TabsTrigger>
    <TabsTrigger value="form">Structured form</TabsTrigger>
  </TabsList>
  <TabsContent value="nl"><NaturalLanguageEntry onExtracted={applyExtraction} /></TabsContent>
  <TabsContent value="form"><StructuredDisputeForm ... /></TabsContent>
</Tabs>
```

Both tabs render the **same** `StructuredDisputeForm`; the NL tab simply sits above it and populates it. Switching tabs never discards entered data. Per Journey 1 the NL tab is the default landing tab.

### 2.4 Natural-language tab & extraction hook

A `Textarea` with a "Extract details" button. On click, calls the extraction endpoint; a loading indicator (spinner + disabled button, `aria-busy`) is shown while awaiting the response (Journey 1 step 5). On success the parent applies the extraction to the form and switches focus to the form for review.

```ts
// src/features/disputes/api.ts
import { useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { ExtractDisputeResponse } from "./types";

export function useExtractDispute() {
  return useMutation({
    mutationFn: async (text: string) =>
      (await api.post<ExtractDisputeResponse>("/ai/extract-dispute", { text })).data,
  });
}
```

```tsx
// src/features/disputes/NaturalLanguageEntry.tsx (excerpt)
const extract = useExtractDispute();
const [text, setText] = useState("");
// <Label htmlFor="nl-text">Describe what happened</Label>
// <Textarea id="nl-text" value={text} onChange={e=>setText(e.target.value)}
//   placeholder="e.g. I was charged R450 twice at Shoprite on 14 July but I only shopped once." />
// <Button aria-busy={extract.isPending} disabled={extract.isPending || text.trim().length < 10}
//   onClick={() => extract.mutate(text, { onSuccess: onExtracted })}>
//   {extract.isPending ? "Extracting…" : "Extract details"}
// </Button>
// extract.isError -> <Alert role="alert">Couldn't read that automatically — please use the structured form.</Alert>
```

Graceful degradation: if extraction fails, the customer is directed to the structured tab (which is always fully functional) — the NL path is an accelerator, never a hard dependency for submission.

### 2.5 Applying extraction + low-confidence highlighting

The `CONFIDENCE_THRESHOLD` is `0.6` (SPEC.md §3.5). Fields returned with confidence `>= 0.6` are set as normal values. Fields **below** threshold, or fields the AI omitted, are treated as "needs review": per AC-DISP-02 they are left blank with a placeholder prompting the customer, AND — where the AI did return a low-confidence guess — the value is still shown but the field is visually flagged (amber ring + "Please confirm" helper text + `aria-describedby`). The customer's raw text is copied into `description`.

```ts
const CONFIDENCE_THRESHOLD = 0.6;

function applyExtraction(res: ExtractDisputeResponse, form: UseFormReturn<DisputeFormValues>) {
  const lowConf = new Set<keyof DisputeFormValues>();
  const setIf = (field: keyof DisputeFormValues, value: unknown, key: keyof ExtractDisputeResponse["confidence"]) => {
    const c = res.confidence[key] ?? 0;
    if (value != null && c >= CONFIDENCE_THRESHOLD) {
      form.setValue(field, String(value), { shouldValidate: true });
    } else {
      // AC-DISP-02: low confidence -> leave for review (blank + prompt), flag the field
      if (value != null) form.setValue(field, String(value));   // show the guess, but flag it
      lowConf.add(field);
    }
  };
  setIf("category", res.category, "category");
  setIf("amount", res.amount, "amount");
  setIf("merchantName", res.merchantName, "merchantName");
  setIf("transactionDate", res.transactionDate, "transactionDate");
  return lowConf;   // drives per-field highlight state
}
```

```tsx
// field wrapper honouring the low-confidence flag
<FormField name="merchantName">
  <FormLabel>Merchant {flagged && <span className="text-amber-600">· please confirm</span>}</FormLabel>
  <Input aria-invalid={flagged || undefined}
         aria-describedby={flagged ? "merchantName-hint" : undefined}
         className={flagged ? "ring-2 ring-amber-500" : undefined}
         placeholder={flagged ? "We weren't sure — please fill this in" : undefined} />
  {flagged && <p id="merchantName-hint" className="text-sm text-amber-700">Auto-filled with low confidence. Please review.</p>}
</FormField>
```

Every extracted/flagged field remains fully editable (AC-DISP-02, DISP-03) — editing a flagged field clears its flag.

### 2.6 Submission

On submit, POST to `/disputes` with the transaction id, the (possibly edited) category, the description, and the reviewed extracted fields. The 201 response yields the reference which drives the confirmation screen. On success, invalidate the `["disputes"]` query so My Disputes (TDP-FE-04) reflects the new dispute.

```ts
export function useSubmitDispute() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      transactionId: string; category: DisputeCategory; description: string;
      extractedFields?: Record<string, unknown>;
    }) => (await api.post<{ id: string; reference: string; status: string }>("/disputes", payload)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["disputes"] }),
  });
}
```

`extractedFields` carries the reviewed values (amount, merchantName, transactionDate, transactionRef) as a JSON object, persisted server-side to `extracted_fields_json` (SPEC.md §3.2). The submit button is disabled while pending and shows a busy state; a failed submission surfaces a retryable, non-destructive error (the form is preserved).

### 2.7 Confirmation screen

On 201, render `DisputeConfirmation` (Journey 1 step 10 / DISP-04): a success `Card` prominently showing the reference (e.g. `DSP-20260714-00042`), a plain-language note that the dispute is being reviewed and will be auto-classified, and actions: **View my disputes** (→ `/my-disputes`) and **Back to transactions**. A polite `aria-live="polite"` region announces success for screen readers.

```tsx
// <Card role="status" aria-live="polite">
//   <CheckCircle aria-hidden /> <h2>Dispute submitted</h2>
//   <p>Your reference is <strong>{reference}</strong>. We'll review it shortly.</p>
//   <Button onClick={()=>navigate("/my-disputes")}>View my disputes</Button>
//   <Button variant="ghost" onClick={()=>navigate("/transactions")}>Back to transactions</Button>
```

## 3. Acceptance Criteria

- The submission page loads bound to the `transactionId` from the query string and shows a summary of the transaction being disputed; a missing/invalid transaction shows a clear error.
- Two tabs are presented — "Describe in your own words" (default) and "Structured form" — and switching tabs preserves entered data (Journey 1 step 3, DISP-01/DISP-02).
- Typing a description and clicking Extract calls `POST /api/v1/ai/extract-dispute`, shows a loading indicator while awaiting the response, and pre-fills the structured form with the returned fields (AI-01, AC-DISP-02, Journey 1 steps 4–6).
- Fields with extraction confidence `< 0.6`, or fields the AI could not determine, are highlighted for review with a prompt and are left blank/flagged per AC-DISP-02; the raw description is retained.
- Every extracted field is editable before submission; editing a flagged field clears its highlight (DISP-03, AC-DISP-02).
- If extraction fails or times out, the customer can still complete and submit via the structured form (graceful degradation; NL path never blocks submission).
- Submitting a valid form calls `POST /api/v1/disputes` and, on 201, shows a confirmation screen displaying the dispute reference in `DSP-YYYYMMDD-NNNNN` format (DISP-04, AC-DISP-04).
- Submit is disabled until required fields (category, description) are valid, and shows a busy state while the request is in flight; a failed submit preserves the form and offers retry.
- After a successful submission the new dispute appears in My Disputes (query invalidated).
- The whole flow is keyboard-operable, tabs and form fields are correctly labelled, low-confidence flags are conveyed by text/ARIA (not colour alone), the loading state sets `aria-busy`, and the confirmation is announced via an `aria-live` region — WCAG 2.1 AA (SPEC.md §3.6).

## 4. Technical Notes

- **Confidence threshold** is `0.6`, matching the backend contract (SPEC.md §3.5). Centralise it in one constant so it stays aligned if the backend changes.
- **AC-DISP-02 nuance:** the spec says low-confidence fields are "left blank with a placeholder". This ticket shows the AI's best guess *and* flags it as low-confidence rather than discarding useful signal — the flag + placeholder satisfy the "prompt the customer to fill it in" intent while remaining editable. Confirm this interpretation with the backend owner; if strict blanking is required, `setIf` clears the value for sub-threshold fields (toggle in one place).
- **Latency:** the extraction endpoint targets < 5s (SPEC.md §3.6). Use the mutation's `isPending` for the loading state; do not set an aggressive client timeout below the backend's — surface a friendly error only after the request genuinely fails.
- **Category enum** must exactly match `UNAUTHORISED | DUPLICATE_CHARGE | MERCHANT_ERROR | WRONG_AMOUNT | OTHER`; render human-readable labels in the `Select` but submit the enum value.
- **No AI key on client:** extraction and summary are server-side only; the frontend never talks to Anthropic directly (SPEC.md §3.6). This UI only calls `/ai/extract-dispute`.
- **Classification is async:** the dispute is created before it is classified (`category`/`priority` are NULL until the background consumer runs, SPEC.md §2.2 step 9, §3.2). The confirmation copy must not promise an immediate category; My Disputes/detail reflects it once classified.
- **Amount parsing:** accept ZAR input, strip currency symbols/thousands separators before sending a numeric `amount`; reject non-numeric.
- **Accessibility:** shadcn `Tabs` (Radix) provide roving-tabindex and correct `role="tab"`/`aria-selected`; ensure focus moves to the form after a successful extraction so keyboard users are placed on the fields to review.

## 5. Definition of Done

- [ ] `DisputeSubmitPage`, `DisputeEntry`, `NaturalLanguageEntry`, `StructuredDisputeForm`, `DisputeConfirmation`, plus `useExtractDispute`/`useSubmitDispute` hooks and the zod schema implemented and merged.
- [ ] Two-tab UI with default NL tab; data preserved across tab switches.
- [ ] Extraction wired to `POST /api/v1/ai/extract-dispute` with loading state, form pre-fill, and low-confidence highlighting/editing per AC-DISP-02.
- [ ] Submission wired to `POST /api/v1/disputes`; confirmation screen shows the `DSP-YYYYMMDD-NNNNN` reference; `["disputes"]` cache invalidated.
- [ ] Graceful degradation verified: extraction failure still allows structured submission.
- [ ] Accessibility verified: tab semantics, labelled fields, non-colour low-confidence indication, `aria-busy` loading, `aria-live` confirmation, full keyboard flow — WCAG 2.1 AA.
- [ ] Component tests (Vitest + RTL) cover: renders both tabs; NL tab calls extract on button click and populates fields; low-confidence field is flagged and editable; submit disabled until required fields valid; confirmation shows reference (feeds TDP-TEST-02).
- [ ] `tsc --noEmit` and ESLint clean; reviewed and merged to `main`; demonstrated end-to-end as Journey 1.
