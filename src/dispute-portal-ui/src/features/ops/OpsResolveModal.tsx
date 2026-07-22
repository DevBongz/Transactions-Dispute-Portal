import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/sonner";
import { useGenerateSummary, useResolveDispute } from "./api";
import { OUTCOME_OPS_LABEL } from "@/lib/labels";
import type { Outcome } from "@/types/api";

const OUTCOMES: Outcome[] = ["UPHELD", "DECLINED", "PARTIAL"];

/**
 * Resolve modal state machine (TDP-FE-05 §2.6): capture (outcome + ≥20-char notes) → generate
 * an editable AI summary → confirm. Confirm is disabled until a summary is present; changing the
 * outcome discards a stale summary so it always matches the chosen outcome. On AI failure the
 * analyst can type the summary manually so an outage never blocks resolution.
 */
export function OpsResolveModal({
  disputeId,
  reference,
  open,
  onOpenChange,
}: {
  disputeId: string;
  reference: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [outcome, setOutcome] = useState<Outcome | "">("");
  const [notes, setNotes] = useState("");
  const [summary, setSummary] = useState("");
  const gen = useGenerateSummary();
  const resolve = useResolveDispute(disputeId);

  const canGenerate = outcome !== "" && notes.trim().length >= 20;
  const canConfirm = summary.trim().length > 0 && !resolve.isPending;

  const reset = () => {
    setOutcome("");
    setNotes("");
    setSummary("");
    gen.reset();
    resolve.reset();
  };

  const close = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  const generate = () => {
    if (outcome === "") return;
    gen.mutate({ disputeId, outcome, internalNotes: notes }, { onSuccess: setSummary });
  };

  const confirm = () => {
    if (outcome === "") return;
    resolve.mutate(
      { outcome, internalNotes: notes, customerSummary: summary },
      {
        onSuccess: () => {
          toast.success(`Dispute ${reference} resolved`);
          close(false);
        },
        onError: () => toast.error("Couldn't resolve the dispute. Please try again."),
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Resolve dispute {reference}</DialogTitle>
          <DialogDescription>
            Record the outcome and your internal notes, generate a customer-facing summary, then confirm.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="outcome">Outcome</Label>
            <Select
              id="outcome"
              value={outcome}
              onChange={(e) => {
                setOutcome(e.target.value as Outcome | "");
                setSummary(""); // stale summary must not survive an outcome change
              }}
            >
              <option value="">Select an outcome…</option>
              {OUTCOMES.map((o) => (
                <option key={o} value={o}>
                  {OUTCOME_OPS_LABEL[o]}
                </option>
              ))}
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="notes">Internal notes</Label>
            <Textarea
              id="notes"
              rows={4}
              value={notes}
              aria-describedby="notes-hint"
              onChange={(e) => setNotes(e.target.value)}
            />
            <p id="notes-hint" className="text-sm text-muted-foreground">
              Minimum 20 characters. Not shown to the customer.
            </p>
          </div>

          <Button
            type="button"
            variant="secondary"
            disabled={!canGenerate || gen.isPending}
            aria-busy={gen.isPending}
            onClick={generate}
          >
            {gen.isPending && <Spinner />}
            {gen.isPending ? "Generating…" : summary ? "Regenerate summary" : "Generate summary"}
          </Button>

          {gen.isError && (
            <Alert variant="destructive">
              <AlertTitle>Summary generation failed</AlertTitle>
              <AlertDescription>
                You can retry, or type the customer summary manually below and still resolve.
              </AlertDescription>
            </Alert>
          )}

          {(summary || gen.isError) && (
            <div className="space-y-2">
              <Label htmlFor="summary">Customer summary (editable)</Label>
              <Textarea
                id="summary"
                rows={5}
                value={summary}
                onChange={(e) => setSummary(e.target.value)}
                placeholder="Plain-language summary shown to the customer."
              />
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => close(false)}>
            Cancel
          </Button>
          <Button disabled={!canConfirm} aria-busy={resolve.isPending} onClick={confirm}>
            {resolve.isPending && <Spinner />}
            {resolve.isPending ? "Resolving…" : "Confirm resolution"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
