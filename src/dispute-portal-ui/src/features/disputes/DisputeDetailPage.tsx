import { useNavigate, useParams } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DataStatesBoundary } from "@/components/DataStates";
import { StatusBadge } from "@/components/StatusBadge";
import { TransactionSummary } from "@/components/TransactionSummary";
import { DisputeTimeline } from "./DisputeTimeline";
import { useDisputeDetail } from "./history-api";
import { CATEGORY_LABEL, OUTCOME_LABEL, PRIORITY_LABEL } from "@/lib/labels";
import { formatDate, formatDateTime } from "@/lib/format";

export function Component() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: d, isLoading, isError, refetch } = useDisputeDetail(id, { pollWhileOpen: true });

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Button variant="ghost" onClick={() => navigate(-1)}>
        ← Back
      </Button>

      <DataStatesBoundary isLoading={isLoading} isError={isError} onRetry={refetch} skeletonRows={5} skeletonCols={2}>
        {d && (
          <>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="font-mono text-2xl font-semibold">{d.reference}</h1>
              <StatusBadge status={d.status} customerView />
              {d.category ? (
                <Badge variant="secondary">{CATEGORY_LABEL[d.category]}</Badge>
              ) : (
                <Badge variant="outline">Pending classification</Badge>
              )}
              {d.priority && <Badge variant="outline">{PRIORITY_LABEL[d.priority]} priority</Badge>}
              <span className="text-sm text-muted-foreground">Submitted {formatDate(d.transaction.transactionDate)}</span>
            </div>

            <TransactionSummary txn={d.transaction} title="Disputed transaction" />

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Your description</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">{d.customerDescription}</p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Progress</CardTitle>
              </CardHeader>
              <CardContent>
                <DisputeTimeline events={d.timeline} />
              </CardContent>
            </Card>

            {d.status === "RESOLVED" && d.resolution && (
              <Card className="border-primary/40 bg-primary/5" role="region" aria-labelledby="resolution-heading">
                <CardHeader>
                  <CardTitle id="resolution-heading">Resolution</CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  <Badge>{OUTCOME_LABEL[d.resolution.outcome]}</Badge>
                  {d.resolution.customerSummary && <p className="mt-2">{d.resolution.customerSummary}</p>}
                  <p className="text-sm text-muted-foreground">
                    Resolved on {formatDateTime(d.resolution.resolvedAt)}
                  </p>
                </CardContent>
              </Card>
            )}
          </>
        )}
      </DataStatesBoundary>
    </div>
  );
}
