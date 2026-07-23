import { setupServer } from "msw/node";
import { http, HttpResponse } from "msw";

// Match extract/summary/submit regardless of host or /api/v1 prefix.
// A machine-local .env (e.g. Render VITE_API_BASE_URL) can change the absolute URL;
// narrow path-prefix patterns then miss and the mutation shows the extract error UI.
export const handlers = [
  http.post(/\/ai\/extract-dispute\/?$/, () =>
    HttpResponse.json({
      transactionRef: null,
      merchantName: "Shoprite",
      amount: 450,
      category: "DUPLICATE_CHARGE",
      transactionDate: "2026-07-14",
      confidence: {
        transactionRef: 0,
        merchantName: 0.95,
        amount: 0.9,
        category: 0.88,
        transactionDate: 0.9,
      },
    }),
  ),
  http.post(/\/ai\/generate-summary\/?$/, () =>
    HttpResponse.json({
      summary: "We reviewed your dispute and refunded the duplicate R450 charge from Shoprite.",
    }),
  ),
  http.post(/\/disputes\/?$/, () =>
    HttpResponse.json(
      { id: "d1", reference: "DSP-20260714-00042", status: "OPEN" },
      { status: 201 },
    ),
  ),
];

export const server = setupServer(...handlers);
