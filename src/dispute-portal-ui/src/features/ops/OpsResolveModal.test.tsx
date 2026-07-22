import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithProviders } from "@/test/test-utils";
import { OpsResolveModal } from "./OpsResolveModal";
import { api } from "@/lib/api-client";

vi.mock("@/lib/api-client", () => ({
  api: { post: vi.fn(), patch: vi.fn(), get: vi.fn() },
}));

const mockedPost = vi.mocked(api.post);

function open() {
  return renderWithProviders(
    <OpsResolveModal disputeId="d1" reference="DSP-20260714-00042" open onOpenChange={() => {}} />,
  );
}

describe("OpsResolveModal", () => {
  beforeEach(() => vi.clearAllMocks());

  it("keeps Generate Summary disabled until an outcome and ≥20-char notes are present (AC-OPS-04)", async () => {
    const user = userEvent.setup();
    open();

    const generate = screen.getByRole("button", { name: /generate summary/i });
    expect(generate).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/outcome/i), "UPHELD");
    await user.type(screen.getByLabelText(/internal notes/i), "too short");
    expect(generate).toBeDisabled();

    await user.clear(screen.getByLabelText(/internal notes/i));
    await user.type(screen.getByLabelText(/internal notes/i), "Refund approved after reviewing the evidence.");
    expect(generate).toBeEnabled();
  });

  it("disables Confirm until a summary exists, and enables it after generation calls the AI endpoint", async () => {
    const user = userEvent.setup();
    mockedPost.mockResolvedValueOnce({ data: { summary: "We have refunded the duplicate charge." } } as never);
    open();

    expect(screen.getByRole("button", { name: /confirm resolution/i })).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/outcome/i), "UPHELD");
    await user.type(screen.getByLabelText(/internal notes/i), "Refund approved after reviewing the evidence.");
    await user.click(screen.getByRole("button", { name: /generate summary/i }));

    expect(mockedPost).toHaveBeenCalledWith(
      "/ai/generate-summary",
      expect.objectContaining({ disputeId: "d1", outcome: "UPHELD" }),
    );

    await waitFor(() =>
      expect(screen.getByLabelText(/customer summary/i)).toHaveValue("We have refunded the duplicate charge."),
    );
    expect(screen.getByRole("button", { name: /confirm resolution/i })).toBeEnabled();
  });
});
