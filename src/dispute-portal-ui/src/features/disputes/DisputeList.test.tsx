import { screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DisputeList } from "./DisputeList";
import type { Dispute } from "@/types/api";

const disputes: Dispute[] = [
  {
    id: "1",
    reference: "DSP-20260714-00001",
    transactionId: "t1",
    customerId: "c1",
    customerName: "Maya",
    status: "OPEN",
    category: "DUPLICATE_CHARGE",
    priority: "HIGH",
    createdAt: "2026-07-14T09:00:00Z",
    updatedAt: "2026-07-14T09:00:00Z",
  },
  {
    id: "2",
    reference: "DSP-20260714-00002",
    transactionId: "t2",
    customerId: "c1",
    customerName: "Maya",
    status: "UNDER_REVIEW",
    category: "UNAUTHORISED",
    priority: "CRITICAL",
    createdAt: "2026-07-14T10:00:00Z",
    updatedAt: "2026-07-14T10:00:00Z",
  },
  {
    id: "3",
    reference: "DSP-20260714-00003",
    transactionId: "t3",
    customerId: "c1",
    customerName: "Maya",
    status: "RESOLVED",
    category: "WRONG_AMOUNT",
    priority: "LOW",
    createdAt: "2026-07-14T11:00:00Z",
    updatedAt: "2026-07-14T11:00:00Z",
  },
];

describe("DisputeList", () => {
  it("renders one row per dispute", () => {
    render(
      <MemoryRouter>
        <DisputeList disputes={disputes} />
      </MemoryRouter>,
    );
    expect(screen.getAllByRole("row")).toHaveLength(disputes.length + 1);
  });

  it("shows the correct status badge text per dispute", () => {
    render(
      <MemoryRouter>
        <DisputeList disputes={disputes} />
      </MemoryRouter>,
    );
    const openRow = screen.getByText("DSP-20260714-00001").closest("tr")!;
    expect(within(openRow).getByText(/open/i)).toBeInTheDocument();
    const reviewRow = screen.getByText("DSP-20260714-00002").closest("tr")!;
    expect(within(reviewRow).getByText(/under review/i)).toBeInTheDocument();
    const resolvedRow = screen.getByText("DSP-20260714-00003").closest("tr")!;
    expect(within(resolvedRow).getByText(/resolved/i)).toBeInTheDocument();
  });

  it("renders an empty state when there are no disputes", () => {
    render(
      <MemoryRouter>
        <DisputeList disputes={[]} />
      </MemoryRouter>,
    );
    expect(screen.getByText(/no disputes/i)).toBeInTheDocument();
  });
});
