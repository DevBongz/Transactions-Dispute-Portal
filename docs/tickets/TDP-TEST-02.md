# TDP-TEST-02 — Frontend Component Tests

**Jira summary:** Establish the frontend component test suite for the Transactions Dispute Portal SPA using Vitest and React Testing Library (RTL). This ticket delivers behaviour-focused tests for the four highest-value components — `DisputeForm` (structured + natural-language tabs), `DisputeList` (status badges), `DisputeTimeline` (chronological events), and `OpsResolveModal` (outcome + AI summary gating) — with the network layer mocked via MSW. It locks the customer submission, tracking, and ops resolution UI flows against regressions and provides the `npm run test` gate consumed by TDP-CICD-01.

## 1. Context & Motivation

- **Background:** After Days 4–6 the SPA (`src/dispute-portal-ui`) is feature-complete: scaffold/auth (TDP-FE-01), transaction views (TDP-FE-02), dispute submission (TDP-FE-03), dispute history & timeline (TDP-FE-04), and the ops dashboard with resolve modal (TDP-FE-05). §4.4 requires component tests for the four listed components. No frontend tests exist yet.
- **Business Impact:** The submission form and resolve modal are where the AI features (extraction, summary) meet the user; their gating logic (submit disabled until required fields present; confirm disabled until a summary exists) directly protects data quality and the §1.1 objectives (≥95% submission success, plain-language summary on every resolution). Tests catch regressions here without a full stack.
- **User Story:** As the developer, I want fast component tests that assert user-visible behaviour so that I can change UI code confidently and keep the customer and ops flows working.
- **Dependencies:** TDP-FE-03, TDP-FE-04, TDP-FE-05 (components under test); TDP-FE-01 (API client, providers). Milestone: **Day 7 (22 Jul)**. Feeds TDP-CICD-01.

## 2. Detailed Description

### 2.1 Tooling & configuration

Stack: **Vitest** (Vite-native runner), **@testing-library/react**, **@testing-library/user-event**, **@testing-library/jest-dom**, **jsdom**, and **MSW** (Mock Service Worker) for HTTP mocking. TanStack Query is already the data layer (per TDP-FE-01), so tests render components inside a fresh `QueryClientProvider`.

```jsonc
// package.json (scripts)
{
  "scripts": {
    "test": "vitest run",
    "test:watch": "vitest",
    "test:ui": "vitest --ui",
    "coverage": "vitest run --coverage"
  },
  "devDependencies": {
    "vitest": "^2.1.0",
    "jsdom": "^25.0.0",
    "@testing-library/react": "^16.0.0",
    "@testing-library/user-event": "^14.5.0",
    "@testing-library/jest-dom": "^6.5.0",
    "@vitest/coverage-v8": "^2.1.0",
    "msw": "^2.4.0"
  }
}
```

```ts
// vitest.config.ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
  resolve: { alias: { "@": path.resolve(__dirname, "./src") } },
});
```

```ts
// src/test/setup.ts
import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "./msw/server";

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

### 2.2 Test utilities — render with providers + MSW

```tsx
// src/test/render.tsx
import { render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ReactElement } from "react";

export function renderWithProviders(ui: ReactElement, { route = "/" } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}
```

```ts
// src/test/msw/server.ts
import { setupServer } from "msw/node";
import { http, HttpResponse } from "msw";

export const handlers = [
  // AI extraction — mirrors POST /api/v1/ai/extract-dispute (SPEC §3.3)
  http.post("*/api/v1/ai/extract-dispute", () =>
    HttpResponse.json({
      merchantName: "Shoprite",
      amount: 450,
      category: "DUPLICATE_CHARGE",
      transactionDate: "2026-07-14",
      confidence: { merchantName: 0.95, amount: 0.9, category: 0.88 },
    }),
  ),
  // AI resolution summary — POST /api/v1/ai/generate-summary
  http.post("*/api/v1/ai/generate-summary", () =>
    HttpResponse.json({ summary: "We reviewed your dispute and refunded the duplicate R450 charge from Shoprite." }),
  ),
  http.post("*/api/v1/disputes", () =>
    HttpResponse.json({ id: "d1", reference: "DSP-20260714-00042", status: "OPEN" }, { status: 201 }),
  ),
];

export const server = setupServer(...handlers);
```

### 2.3 `DisputeForm` (TDP-FE-03)

Per §4.4: renders structured and NL tabs; NL tab calls the extract endpoint on button click; submission disabled until required fields populated (AC-DISP-02).

```tsx
// src/components/disputes/DisputeForm.test.tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { DisputeForm } from "./DisputeForm";

const txn = { id: "txn-1", reference: "TXN-20260714-00001", merchantName: "Shoprite", amount: 450, currency: "ZAR" };

describe("DisputeForm", () => {
  it("renders both the structured form and natural-language tabs", () => {
    renderWithProviders(<DisputeForm transaction={txn} />);
    expect(screen.getByRole("tab", { name: /structured form/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /own words/i })).toBeInTheDocument();
  });

  it("calls the AI extract endpoint and pre-fills fields from the NL description", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DisputeForm transaction={txn} />);

    await user.click(screen.getByRole("tab", { name: /own words/i }));
    await user.type(
      screen.getByRole("textbox", { name: /describe/i }),
      "I was charged R450 twice at Shoprite on 14 July but I only shopped once.",
    );
    await user.click(screen.getByRole("button", { name: /extract|analyse|analyze/i }));

    // MSW returns Shoprite/450/DUPLICATE_CHARGE — assert fields are populated for review
    await waitFor(() =>
      expect(screen.getByLabelText(/merchant/i)).toHaveValue("Shoprite"),
    );
    expect(screen.getByLabelText(/amount/i)).toHaveValue(450);
  });

  it("keeps submit disabled until required fields are populated", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DisputeForm transaction={txn} />);

    const submit = screen.getByRole("button", { name: /submit/i });
    expect(submit).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/reason|category/i), "DUPLICATE_CHARGE");
    await user.type(screen.getByLabelText(/description/i), "Charged twice for one purchase.");
    expect(submit).toBeEnabled();
  });
});
```

### 2.4 `DisputeList` (TDP-FE-04)

Per §4.4: renders dispute rows with correct status badges. Status values per §3.2: `OPEN`, `UNDER_REVIEW`, `RESOLVED`, `CLASSIFICATION_FAILED`.

```tsx
// src/components/disputes/DisputeList.test.tsx
import { screen, within } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { DisputeList } from "./DisputeList";

const disputes = [
  { id: "1", reference: "DSP-20260714-00001", status: "OPEN", category: "DUPLICATE_CHARGE", priority: "HIGH" },
  { id: "2", reference: "DSP-20260714-00002", status: "UNDER_REVIEW", category: "UNAUTHORISED", priority: "CRITICAL" },
  { id: "3", reference: "DSP-20260714-00003", status: "RESOLVED", category: "WRONG_AMOUNT", priority: "LOW" },
];

describe("DisputeList", () => {
  it("renders one row per dispute", () => {
    renderWithProviders(<DisputeList disputes={disputes} />);
    expect(screen.getAllByRole("row")).toHaveLength(disputes.length + 1); // + header
  });

  it("shows the correct status badge text per dispute", () => {
    renderWithProviders(<DisputeList disputes={disputes} />);
    const openRow = screen.getByText("DSP-20260714-00001").closest("tr")!;
    expect(within(openRow).getByText(/open/i)).toBeInTheDocument();
    const reviewRow = screen.getByText("DSP-20260714-00002").closest("tr")!;
    expect(within(reviewRow).getByText(/under review/i)).toBeInTheDocument();
    const resolvedRow = screen.getByText("DSP-20260714-00003").closest("tr")!;
    expect(within(resolvedRow).getByText(/resolved/i)).toBeInTheDocument();
  });

  it("renders an empty state when there are no disputes", () => {
    renderWithProviders(<DisputeList disputes={[]} />);
    expect(screen.getByText(/no disputes/i)).toBeInTheDocument();
  });
});
```

### 2.5 `DisputeTimeline` (TDP-FE-04)

Per §4.4: renders events in chronological order. Event types per §3.2: `SUBMITTED`, `CLASSIFIED`, `ASSIGNED`, `UNDER_REVIEW`, `RESOLVED`.

```tsx
// src/components/disputes/DisputeTimeline.test.tsx
import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { DisputeTimeline } from "./DisputeTimeline";

const events = [
  { id: "e1", eventType: "SUBMITTED", description: "Dispute submitted", createdAt: "2026-07-14T09:00:00Z" },
  { id: "e2", eventType: "CLASSIFIED", description: "Classified as DUPLICATE_CHARGE / HIGH", createdAt: "2026-07-14T09:00:04Z" },
  { id: "e3", eventType: "UNDER_REVIEW", description: "Assigned to analyst", createdAt: "2026-07-15T10:30:00Z" },
  { id: "e4", eventType: "RESOLVED", description: "Resolved: UPHELD", createdAt: "2026-07-16T14:00:00Z" },
];

describe("DisputeTimeline", () => {
  it("renders events oldest-to-newest regardless of input order", () => {
    const shuffled = [events[3], events[0], events[2], events[1]];
    renderWithProviders(<DisputeTimeline events={shuffled} />);

    const rendered = screen.getAllByTestId("timeline-item");
    const order = rendered.map((el) => el.getAttribute("data-event-type"));
    expect(order).toEqual(["SUBMITTED", "CLASSIFIED", "UNDER_REVIEW", "RESOLVED"]);
  });

  it("renders a human-readable description for each event", () => {
    renderWithProviders(<DisputeTimeline events={events} />);
    expect(screen.getByText(/dispute submitted/i)).toBeInTheDocument();
    expect(screen.getByText(/resolved: upheld/i)).toBeInTheDocument();
  });
});
```

> Requires each timeline node to carry `data-testid="timeline-item"` and `data-event-type={eventType}`; add these to `DisputeTimeline` (TDP-FE-04) if not present.

### 2.6 `OpsResolveModal` (TDP-FE-05)

Per §4.4: outcome dropdown required; generate-summary button calls the AI endpoint; confirm button disabled until summary present (AC-OPS-04/AC-AI-03). Outcomes per §3.2: `UPHELD`, `DECLINED`, `PARTIAL`; internal notes minimum 20 characters (AC-OPS-04).

```tsx
// src/components/ops/OpsResolveModal.test.tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { OpsResolveModal } from "./OpsResolveModal";

const dispute = { id: "d1", reference: "DSP-20260714-00042" };

describe("OpsResolveModal", () => {
  it("disables Generate Summary until an outcome and sufficient notes are provided", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OpsResolveModal open dispute={dispute} onResolved={() => {}} />);

    const generate = screen.getByRole("button", { name: /generate summary/i });
    expect(generate).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/outcome/i), "UPHELD");
    await user.type(screen.getByLabelText(/internal notes/i), "Confirmed duplicate charge; refund initiated.");
    expect(generate).toBeEnabled();
  });

  it("calls the AI summary endpoint and shows the returned summary for review", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OpsResolveModal open dispute={dispute} onResolved={() => {}} />);

    await user.selectOptions(screen.getByLabelText(/outcome/i), "UPHELD");
    await user.type(screen.getByLabelText(/internal notes/i), "Confirmed duplicate charge; refund initiated.");
    await user.click(screen.getByRole("button", { name: /generate summary/i }));

    await waitFor(() =>
      expect(screen.getByDisplayValue(/refunded the duplicate R450 charge/i)).toBeInTheDocument(),
    );
  });

  it("keeps Confirm Resolution disabled until a summary is present", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OpsResolveModal open dispute={dispute} onResolved={() => {}} />);

    const confirm = screen.getByRole("button", { name: /confirm resolution/i });
    expect(confirm).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/outcome/i), "UPHELD");
    await user.type(screen.getByLabelText(/internal notes/i), "Confirmed duplicate charge; refund initiated.");
    await user.click(screen.getByRole("button", { name: /generate summary/i }));
    await waitFor(() => expect(confirm).toBeEnabled());
  });
});
```

### 2.7 Running

```bash
cd src/dispute-portal-ui
npm ci
npm run test          # vitest run — CI mode, exits non-zero on failure
npm run test:watch    # local TDD
npm run coverage      # v8 coverage report
```

## 3. Acceptance Criteria

- Vitest configured with the jsdom environment, a global setup that starts/stops MSW, and `npm run test` (`vitest run`) exits non-zero on any failure.
- MSW handlers mock `/api/v1/ai/extract-dispute`, `/api/v1/ai/generate-summary`, and `/api/v1/disputes` with realistic payloads; unhandled requests error the test (no accidental live calls).
- **DisputeForm:** tests assert both tabs render; clicking the NL extract button triggers the extract endpoint and pre-fills the form for review; submit stays disabled until required fields are populated (AC-DISP-02).
- **DisputeList:** tests assert one row per dispute, correct status-badge text for `OPEN` / `UNDER_REVIEW` / `RESOLVED`, and an empty state.
- **DisputeTimeline:** tests assert events render oldest-to-newest regardless of input order and that each event description is shown.
- **OpsResolveModal:** tests assert the outcome dropdown gates Generate Summary, the summary endpoint is called and its text shown for review, and Confirm Resolution is disabled until a summary is present (AC-OPS-04/AC-AI-03).
- Tests query by accessible role/label (RTL best practice) rather than implementation details, reinforcing the §3.6 WCAG 2.1 AA accessibility expectation.
- `npm run test` passes locally in `src/dispute-portal-ui`.

## 4. Technical Notes

- **MSW over fetch stubs:** MSW intercepts at the network layer, so the real API client and TanStack Query cache behaviour are exercised. Set `onUnhandledRequest: "error"` to catch endpoints the component calls but tests forgot to mock.
- **Disable query/mutation retries** in the test `QueryClient` (`retry: false`) or error-path assertions hang for the default retry/backoff window.
- **`userEvent.setup()`** per test (userEvent v14) — do not use the deprecated direct `userEvent.click` import style; async `await` every interaction.
- **Accessible selectors depend on markup:** label associations (`htmlFor`/`aria-label`) and `role="tab"` must exist on the components. Where shadcn/ui `Select` renders a non-native control, the test may need `getByRole("combobox")` + option click rather than `selectOptions`; align the test with the actual primitive used in TDP-FE-05.
- **Timeline ordering hook:** the chronological-order test relies on `data-testid="timeline-item"` + `data-event-type`; coordinate with TDP-FE-04 to add these attributes.
- **No real backend/AI:** all HTTP is mocked; tests never require `ANTHROPIC_API_KEY` or a running API, keeping them fast and CI-friendly (TDP-CICD-01).
- **Coverage is informational**, not a hard gate for the submission timeline; focus effort on the four mandated components rather than chasing a percentage.

## 5. Definition of Done

- [ ] Vitest + RTL + MSW dev-dependencies added; `vitest.config.ts` and `src/test/setup.ts` committed.
- [ ] `renderWithProviders` helper and MSW server/handlers committed.
- [ ] Test files for `DisputeForm`, `DisputeList`, `DisputeTimeline`, and `OpsResolveModal` implemented per §2.3–2.6.
- [ ] Any required test hooks (`data-testid`, label associations) added to the components without changing their visible behaviour.
- [ ] `npm run test` passes locally with zero failures and no unhandled-request errors.
- [ ] Test commands documented in the README (coordinate with TDP-DOC-02) and wired into CI (TDP-CICD-01).
- [ ] Code reviewed and merged to `main`.
