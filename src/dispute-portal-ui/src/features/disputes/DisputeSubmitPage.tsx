import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { TransactionSummary } from "@/components/TransactionSummary";
import { TableSkeleton } from "@/components/DataStates";
import { useTransaction } from "@/features/transactions/api";
import { DisputeEntry } from "./DisputeEntry";
import { DisputeConfirmation } from "./DisputeConfirmation";

export function Component() {
  const [sp] = useSearchParams();
  const transactionId = sp.get("transactionId") ?? "";
  const { data: txn, isLoading, isError } = useTransaction(transactionId);
  const [reference, setReference] = useState<string>();

  if (!transactionId || isError) {
    return (
      <div className="mx-auto max-w-2xl">
        <Alert variant="destructive">
          <AlertTitle>No transaction selected</AlertTitle>
          <AlertDescription>
            Start a dispute from a transaction.{" "}
            <Button asChild variant="link" className="h-auto p-0">
              <Link to="/transactions">Go to transactions</Link>
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">Raise a dispute</h1>

      {isLoading && <TableSkeleton rows={3} cols={2} />}

      {txn && (
        <>
          <TransactionSummary txn={txn} title="Disputing this transaction" />
          {reference ? (
            <DisputeConfirmation reference={reference} />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Tell us what went wrong</CardTitle>
              </CardHeader>
              <CardContent>
                <DisputeEntry transaction={txn} onSubmitted={setReference} />
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
