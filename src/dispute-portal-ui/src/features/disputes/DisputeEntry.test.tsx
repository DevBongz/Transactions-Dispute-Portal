import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "@/test/test-utils";
import { DisputeEntry } from "./DisputeEntry";
import type { Transaction } from "@/types/api";

const txn: Transaction = {
  id: "txn-1",
  reference: "TXN-20260714-00001",
  merchantName: "Shoprite",
  merchantCategory: "Grocery Stores",
  amount: 450,
  currency: "ZAR",
  transactionDate: "2026-07-14T10:00:00Z",
  status: "SETTLED",
};

describe("DisputeEntry (DisputeForm)", () => {
  it("renders both the structured form and natural-language tabs", () => {
    renderWithProviders(<DisputeEntry transaction={txn} onSubmitted={() => {}} />);
    expect(screen.getByRole("tab", { name: /own words/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /structured form/i })).toBeInTheDocument();
  });

  it("calls the AI extract endpoint and pre-fills fields from the NL description", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DisputeEntry transaction={txn} onSubmitted={() => {}} />);

    await user.click(screen.getByRole("tab", { name: /own words/i }));
    await user.type(
      screen.getByLabelText(/describe what happened/i),
      "I was charged R450 twice at Shoprite on 14 July but I only shopped once.",
    );
    await user.click(screen.getByRole("button", { name: /extract details/i }));

    await waitFor(() => expect(screen.getByLabelText(/merchant/i)).toHaveValue("Shoprite"));
    expect(screen.getByLabelText(/^amount/i)).toHaveValue("450");
    expect(screen.getByLabelText(/category/i)).toHaveValue("DUPLICATE_CHARGE");
  });

  it("keeps submit disabled until required fields are populated", async () => {
    const user = userEvent.setup();
    renderWithProviders(<DisputeEntry transaction={txn} onSubmitted={() => {}} />);

    await user.click(screen.getByRole("tab", { name: /structured form/i }));
    const submit = screen.getByRole("button", { name: /submit dispute/i });
    expect(submit).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/category/i), "DUPLICATE_CHARGE");
    await user.type(screen.getByLabelText(/what happened/i), "Charged twice for one purchase.");
    expect(submit).toBeEnabled();
  });
});
