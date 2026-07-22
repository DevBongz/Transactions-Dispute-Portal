import { useState } from "react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { NaturalLanguageEntry } from "./NaturalLanguageEntry";
import { StructuredDisputeForm } from "./StructuredDisputeForm";
import { useSubmitDispute, type SubmitDisputePayload } from "./api";
import { CONFIDENCE_THRESHOLD, EMPTY_FORM, type DisputeFormField, type DisputeFormValues } from "./types";
import type { DisputeCategory, ExtractDisputeResponse, Transaction } from "@/types/api";

const VALID_CATEGORIES: DisputeCategory[] = [
  "UNAUTHORISED",
  "DUPLICATE_CHARGE",
  "MERCHANT_ERROR",
  "WRONG_AMOUNT",
  "OTHER",
];

function toDateInput(raw: string): string {
  const m = /^\d{4}-\d{2}-\d{2}/.exec(raw);
  return m ? m[0] : raw;
}

/**
 * Two-tab entry (TDP-FE-03 §2.3). Both tabs drive the SAME form state so switching never
 * loses data. The NL tab pre-fills the form and flags low-confidence / omitted fields for
 * review per AC-DISP-02, then focus moves to the form for the customer to confirm.
 */
export function DisputeEntry({
  transaction,
  onSubmitted,
}: {
  transaction: Transaction;
  onSubmitted: (reference: string) => void;
}) {
  const [tab, setTab] = useState("nl");
  const [form, setForm] = useState<DisputeFormValues>(EMPTY_FORM);
  const [lowConf, setLowConf] = useState<Set<DisputeFormField>>(new Set());
  const [transactionRef, setTransactionRef] = useState<string | undefined>();
  const submit = useSubmitDispute();

  const setField = (field: DisputeFormField, val: string) => {
    setForm((f) => ({ ...f, [field]: val }));
    // Editing a flagged field clears its flag (AC-DISP-02).
    setLowConf((prev) => {
      if (!prev.has(field)) return prev;
      const next = new Set(prev);
      next.delete(field);
      return next;
    });
  };

  const applyExtraction = (res: ExtractDisputeResponse, rawText: string) => {
    const next: Record<DisputeFormField, string> = { ...EMPTY_FORM, description: rawText };
    const flags = new Set<DisputeFormField>();

    const apply = (
      field: DisputeFormField,
      value: string | null | undefined,
      key: keyof ExtractDisputeResponse["confidence"],
    ) => {
      const c = res.confidence[key] ?? 0;
      if (value != null && value !== "") {
        next[field] = value;
        if (c < CONFIDENCE_THRESHOLD) flags.add(field);
      } else {
        // AI omitted the field — leave blank and flag for review.
        flags.add(field);
      }
    };

    const category =
      res.category && VALID_CATEGORIES.includes(res.category) ? res.category : null;
    apply("category", category, "category");
    apply("amount", res.amount != null ? String(res.amount) : null, "amount");
    apply("merchantName", res.merchantName ?? null, "merchantName");
    apply("transactionDate", res.transactionDate ? toDateInput(res.transactionDate) : null, "transactionDate");

    setForm(next as DisputeFormValues);
    setLowConf(flags);
    setTransactionRef(res.transactionRef ?? undefined);
    setTab("form");
    // Move focus to the first field to review (keyboard users land on the form).
    setTimeout(() => document.getElementById("category")?.focus(), 0);
  };

  const onSubmit = () => {
    if (form.category === "") return;
    const extractedFields: Record<string, unknown> = {};
    if (form.amount) extractedFields.amount = Number(form.amount);
    if (form.merchantName) extractedFields.merchantName = form.merchantName;
    if (form.transactionDate) extractedFields.transactionDate = form.transactionDate;
    if (transactionRef) extractedFields.transactionRef = transactionRef;

    const payload: SubmitDisputePayload = {
      transactionId: transaction.id,
      category: form.category,
      description: form.description,
      extractedFields: Object.keys(extractedFields).length ? extractedFields : undefined,
    };
    submit.mutate(payload, { onSuccess: (r) => onSubmitted(r.reference) });
  };

  return (
    <Tabs value={tab} onValueChange={setTab}>
      <TabsList aria-label="Dispute entry method">
        <TabsTrigger value="nl">Describe in your own words</TabsTrigger>
        <TabsTrigger value="form">Structured form</TabsTrigger>
      </TabsList>

      <TabsContent value="nl">
        <NaturalLanguageEntry onExtracted={applyExtraction} />
        <p className="mt-4 text-sm text-muted-foreground">
          Prefer to type it in yourself? Switch to the structured form above at any time.
        </p>
      </TabsContent>

      <TabsContent value="form">
        <StructuredDisputeForm
          value={form}
          onField={setField}
          lowConf={lowConf}
          onSubmit={onSubmit}
          isSubmitting={submit.isPending}
          submitError={submit.isError}
        />
      </TabsContent>
    </Tabs>
  );
}
