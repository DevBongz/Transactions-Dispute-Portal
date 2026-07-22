import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { useExtractDispute } from "./api";
import type { ExtractDisputeResponse } from "@/types/api";

/**
 * "Describe in your own words" tab (TDP-FE-03 §2.4). Sends free text to the extraction
 * endpoint, shows a busy state, then hands the result up to be applied to the shared form.
 * The raw text is always forwarded so it can seed the description regardless of extraction.
 */
export function NaturalLanguageEntry({
  onExtracted,
}: {
  onExtracted: (res: ExtractDisputeResponse, rawText: string) => void;
}) {
  const extract = useExtractDispute();
  const [text, setText] = useState("");

  const run = () => {
    extract.mutate(text, { onSuccess: (res) => onExtracted(res, text) });
  };

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="nl-text">Describe what happened</Label>
        <Textarea
          id="nl-text"
          rows={5}
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="e.g. I was charged R450 twice at Shoprite on 14 July but I only shopped once."
        />
      </div>

      <Button
        type="button"
        aria-busy={extract.isPending}
        disabled={extract.isPending || text.trim().length < 10}
        onClick={run}
      >
        {extract.isPending && <Spinner />}
        {extract.isPending ? "Extracting…" : "Extract details"}
      </Button>

      {extract.isError && (
        <Alert variant="destructive">
          <AlertTitle>Couldn't read that automatically</AlertTitle>
          <AlertDescription>
            Please switch to the structured form and fill in the details yourself — your text has been kept.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
