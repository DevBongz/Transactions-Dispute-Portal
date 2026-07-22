import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { StructuredDisputeForm } from "./StructuredDisputeForm";
import { EMPTY_FORM, type DisputeFormField, type DisputeFormValues } from "./types";

function Harness({ onSubmit }: { onSubmit: () => void }) {
  const [form, setForm] = useState<DisputeFormValues>(EMPTY_FORM);
  const [lowConf, setLowConf] = useState(new Set<DisputeFormField>(["merchantName"]));
  return (
    <StructuredDisputeForm
      value={form}
      onField={(f, v) => {
        setForm((s) => ({ ...s, [f]: v }));
        setLowConf((prev) => {
          const next = new Set(prev);
          next.delete(f);
          return next;
        });
      }}
      lowConf={lowConf}
      onSubmit={onSubmit}
      isSubmitting={false}
      submitError={false}
    />
  );
}

describe("StructuredDisputeForm", () => {
  it("disables submit until category and a valid description are provided", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} />);

    const submit = screen.getByRole("button", { name: /submit dispute/i });
    expect(submit).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/category/i), "DUPLICATE_CHARGE");
    await user.type(screen.getByLabelText(/what happened/i), "Charged twice for one purchase.");

    expect(submit).toBeEnabled();
    await user.click(submit);
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("flags a low-confidence field and clears the flag once edited (AC-DISP-02)", async () => {
    const user = userEvent.setup();
    render(<Harness onSubmit={() => {}} />);

    const merchant = screen.getByLabelText(/merchant/i);
    expect(merchant).toHaveAttribute("aria-invalid", "true");

    await user.type(merchant, "Shoprite");
    expect(merchant).not.toHaveAttribute("aria-invalid");
  });
});
