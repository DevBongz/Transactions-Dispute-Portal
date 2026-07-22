import { useNavigate } from "react-router-dom";
import { CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/** Success screen (TDP-FE-03 §2.7 / DISP-04). Announced politely for screen readers. */
export function DisputeConfirmation({ reference }: { reference: string }) {
  const navigate = useNavigate();
  return (
    <Card role="status" aria-live="polite" className="border-primary/40 bg-primary/5">
      <CardHeader>
        <div className="flex items-center gap-2">
          <CheckCircle2 className="h-6 w-6 text-primary" aria-hidden />
          <CardTitle>Dispute submitted</CardTitle>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <p>
          Your reference is <strong className="font-mono">{reference}</strong>. We'll review it shortly and
          categorise it automatically — you can follow its progress in My Disputes.
        </p>
        <div className="flex flex-wrap gap-3">
          <Button onClick={() => navigate("/my-disputes")}>View my disputes</Button>
          <Button variant="ghost" onClick={() => navigate("/transactions")}>
            Back to transactions
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
