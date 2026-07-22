import type { ReactNode } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { formatCurrency, formatDateTime } from "@/lib/format";
import type { Transaction } from "@/types/api";

const TXN_STATUS_LABEL: Record<Transaction["status"], string> = {
  SETTLED: "Settled",
  PENDING: "Pending",
  REVERSED: "Reversed",
};

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{value}</dd>
    </div>
  );
}

/** Shared read-only transaction panel (reused by FE-02/03/04/05). */
export function TransactionSummary({ txn, title = "Transaction" }: { txn: Transaction; title?: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <Field label="Merchant" value={txn.merchantName} />
          <Field label="Category" value={txn.merchantCategory ?? "—"} />
          <Field label="Amount" value={formatCurrency(txn.amount, txn.currency)} />
          <Field label="Reference" value={<span className="font-mono">{txn.reference}</span>} />
          <Field label="Date" value={formatDateTime(txn.transactionDate)} />
          <Field label="Status" value={<Badge variant="outline">{TXN_STATUS_LABEL[txn.status]}</Badge>} />
        </dl>
      </CardContent>
    </Card>
  );
}
