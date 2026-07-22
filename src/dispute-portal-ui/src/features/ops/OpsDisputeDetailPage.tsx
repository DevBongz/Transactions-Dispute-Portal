import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DataStatesBoundary } from "@/components/DataStates";
import { StatusBadge } from "@/components/StatusBadge";
import { PriorityBadge } from "@/components/PriorityBadge";
import { TransactionSummary } from "@/components/TransactionSummary";
import { toast } from "@/components/ui/sonner";
import { DisputeTimeline } from "@/features/disputes/DisputeTimeline";
import { useDisputeDetail } from "@/features/disputes/history-api";
import { OpsResolveModal } from "./OpsResolveModal";
import { useUpdateStatus } from "./api";
import { CATEGORY_LABEL, OUTCOME_OPS_LABEL } from "@/lib/labels";
import { formatDateTime } from "@/lib/format";

export function Component() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: d, isLoading, isError, refetch } = useDisputeDetail(id);
  const updateStatus = useUpdateStatus(id);
  const [resolveOpen, setResolveOpen] = useState(false);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <Button variant="ghost" onClick={() => navigate(-1)}>
        ← Back to queue
      </Button>

      <DataStatesBoundary isLoading={isLoading} isError={isError} onRetry={refetch} skeletonRows={6} skeletonCols={2}>
        {d && (
          <>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="font-mono text-2xl font-semibold">{d.reference}</h1>
              <StatusBadge status={d.status} />
              <PriorityBadge priority={d.priority} />
            </div>

            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Customer</CardTitle>
                </CardHeader>
                <CardContent className="space-y-1">
                  <p className="font-medium">{d.customerName}</p>
                  <p className="text-sm text-muted-foreground">{d.customerEmail}</p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-base">AI classification</CardTitle>
                </CardHeader>
                <CardContent className="flex flex-wrap items-center gap-2">
                  {d.status === "CLASSIFICATION_FAILED" ? (
                    <Badge variant="destructive">Needs manual triage</Badge>
                  ) : d.category ? (
                    <>
                      <Badge variant="secondary">{CATEGORY_LABEL[d.category]}</Badge>
                      <PriorityBadge priority={d.priority} />
                    </>
                  ) : (
                    <Badge variant="outline">Pending classification</Badge>
                  )}
                </CardContent>
              </Card>
            </div>

            <TransactionSummary txn={d.transaction} title="Original transaction" />

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Customer's description</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">{d.customerDescription}</p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Timeline</CardTitle>
              </CardHeader>
              <CardContent>
                <DisputeTimeline events={d.timeline} />
              </CardContent>
            </Card>

            {d.status === "RESOLVED" && d.resolution ? (
              <Card className="border-primary/40 bg-primary/5" role="region" aria-labelledby="ops-resolution">
                <CardHeader>
                  <CardTitle id="ops-resolution" className="text-base">
                    Resolution
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  <Badge>{OUTCOME_OPS_LABEL[d.resolution.outcome]}</Badge>
                  {d.resolution.customerSummary && <p className="mt-2 text-sm">{d.resolution.customerSummary}</p>}
                  <p className="text-sm text-muted-foreground">Resolved on {formatDateTime(d.resolution.resolvedAt)}</p>
                </CardContent>
              </Card>
            ) : (
              <div className="flex flex-wrap gap-3">
                {d.status === "OPEN" && (
                  <Button
                    variant="outline"
                    disabled={updateStatus.isPending}
                    onClick={() =>
                      updateStatus.mutate("UNDER_REVIEW", {
                        onSuccess: () => toast.success("Marked under review"),
                        onError: () => toast.error("Couldn't update status"),
                      })
                    }
                  >
                    Start review
                  </Button>
                )}
                <Button onClick={() => setResolveOpen(true)}>Resolve dispute</Button>
              </div>
            )}

            <OpsResolveModal
              disputeId={d.id}
              reference={d.reference}
              open={resolveOpen}
              onOpenChange={setResolveOpen}
            />
          </>
        )}
      </DataStatesBoundary>
    </div>
  );
}
