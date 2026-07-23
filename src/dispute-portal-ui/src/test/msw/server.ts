import { setupServer } from "msw/node";
import { http, HttpResponse } from "msw";

/** Default handlers for the four high-value UI flows (TDP-TEST-02). */
export const handlers = [
  http.post("*/api/v1/ai/extract-dispute", () =>
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
  http.post("*/api/v1/ai/generate-summary", () =>
    HttpResponse.json({
      summary: "We reviewed your dispute and refunded the duplicate R450 charge from Shoprite.",
    }),
  ),
  http.post("*/api/v1/disputes", () =>
    HttpResponse.json(
      { id: "d1", reference: "DSP-20260714-00042", status: "OPEN" },
      { status: 201 },
    ),
  ),
];

export const server = setupServer(...handlers);
