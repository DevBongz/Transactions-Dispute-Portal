import { useNavigate, useParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { TransactionSummary } from "@/components/TransactionSummary";
import { DataStatesBoundary } from "@/components/DataStates";
import { useTransaction } from "./api";

export function Component() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: txn, isLoading, isError, refetch } = useTransaction(id);

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <Button variant="ghost" onClick={() => navigate(-1)}>
        ← Back
      </Button>

      <DataStatesBoundary isLoading={isLoading} isError={isError} onRetry={refetch} skeletonRows={4} skeletonCols={2}>
        {txn && (
          <>
            <TransactionSummary txn={txn} title="Transaction detail" />
            <Button
              size="lg"
              className="w-full"
              disabled={txn.hasDispute}
              onClick={() => navigate(`/disputes/new?transactionId=${txn.id}`)}
            >
              {txn.hasDispute ? "Dispute already raised" : "Dispute this transaction"}
            </Button>
          </>
        )}
      </DataStatesBoundary>
    </div>
  );
}
