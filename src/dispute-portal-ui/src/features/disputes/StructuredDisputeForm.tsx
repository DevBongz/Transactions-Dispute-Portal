import { type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { CATEGORY_OPTIONS } from "@/lib/labels";
import { cn } from "@/lib/utils";
import type { DisputeFormField, DisputeFormValues } from "./types";

/** Strip currency symbols / thousands separators, returning a numeric string or "". */
export function parseAmount(raw: string): string {
  const cleaned = raw.replace(/[^0-9.]/g, "");
  return cleaned;
}

function flaggedProps(flagged: boolean, id: string) {
  return flagged
    ? { "aria-invalid": true, "aria-describedby": `${id}-hint`, className: "ring-2 ring-amber-500" }
    : {};
}

interface Props {
  value: DisputeFormValues;
  onField: (field: DisputeFormField, val: string) => void;
  lowConf: Set<DisputeFormField>;
  onSubmit: () => void;
  isSubmitting: boolean;
  submitError: boolean;
}

/**
 * The structured dispute form (shared by both entry tabs, TDP-FE-03 §2.3). Required fields are
 * category + description; low-confidence AI fields are visually flagged and, per AC-DISP-02,
 * editing a flagged field clears its flag. Submission is disabled until required fields validate.
 */
export function StructuredDisputeForm({ value, onField, lowConf, onSubmit, isSubmitting, submitError }: Props) {
  const canSubmit = value.category !== "" && value.description.trim().length >= 10;

  const handle = (e: FormEvent) => {
    e.preventDefault();
    if (canSubmit) onSubmit();
  };

  const FlagHint = ({ field }: { field: DisputeFormField }) =>
    lowConf.has(field) ? (
      <p id={`${field}-hint`} className="text-sm text-amber-700">
        Auto-filled with low confidence. Please review.
      </p>
    ) : null;

  return (
    <form onSubmit={handle} className="space-y-5" noValidate>
      <div className="space-y-2">
        <Label htmlFor="category">
          Category <span className="text-destructive">*</span>
          {lowConf.has("category") && <span className="ml-1 text-amber-600">· please confirm</span>}
        </Label>
        <Select
          id="category"
          value={value.category}
          onChange={(e) => onField("category", e.target.value)}
          {...flaggedProps(lowConf.has("category"), "category")}
        >
          <option value="">Select a category…</option>
          {CATEGORY_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </Select>
        <FlagHint field="category" />
      </div>

      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="amount">
            Amount (ZAR)
            {lowConf.has("amount") && <span className="ml-1 text-amber-600">· please confirm</span>}
          </Label>
          <Input
            id="amount"
            inputMode="decimal"
            value={value.amount}
            placeholder={lowConf.has("amount") ? "We weren't sure — please fill this in" : undefined}
            onChange={(e) => onField("amount", parseAmount(e.target.value))}
            {...flaggedProps(lowConf.has("amount"), "amount")}
          />
          <FlagHint field="amount" />
        </div>

        <div className="space-y-2">
          <Label htmlFor="transactionDate">
            Transaction date
            {lowConf.has("transactionDate") && <span className="ml-1 text-amber-600">· please confirm</span>}
          </Label>
          <Input
            id="transactionDate"
            type="date"
            value={value.transactionDate}
            onChange={(e) => onField("transactionDate", e.target.value)}
            {...flaggedProps(lowConf.has("transactionDate"), "transactionDate")}
          />
          <FlagHint field="transactionDate" />
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="merchantName">
          Merchant
          {lowConf.has("merchantName") && <span className="ml-1 text-amber-600">· please confirm</span>}
        </Label>
        <Input
          id="merchantName"
          value={value.merchantName}
          placeholder={lowConf.has("merchantName") ? "We weren't sure — please fill this in" : undefined}
          onChange={(e) => onField("merchantName", e.target.value)}
          {...flaggedProps(lowConf.has("merchantName"), "merchantName")}
        />
        <FlagHint field="merchantName" />
      </div>

      <div className="space-y-2">
        <Label htmlFor="description">
          What happened? <span className="text-destructive">*</span>
        </Label>
        <Textarea
          id="description"
          rows={5}
          value={value.description}
          aria-describedby="description-hint"
          onChange={(e) => onField("description", e.target.value)}
        />
        <p id="description-hint" className={cn("text-sm", value.description.trim().length < 10 ? "text-muted-foreground" : "text-muted-foreground")}>
          Describe the problem in a sentence or two (minimum 10 characters).
        </p>
      </div>

      {submitError && (
        <Alert variant="destructive">
          <AlertTitle>Submission failed</AlertTitle>
          <AlertDescription>Your details are preserved — please try submitting again.</AlertDescription>
        </Alert>
      )}

      <Button type="submit" size="lg" disabled={!canSubmit || isSubmitting} aria-busy={isSubmitting}>
        {isSubmitting && <Spinner />}
        {isSubmitting ? "Submitting…" : "Submit dispute"}
      </Button>
    </form>
  );
}
