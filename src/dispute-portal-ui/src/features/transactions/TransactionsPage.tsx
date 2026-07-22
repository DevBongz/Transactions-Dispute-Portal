import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { DataStatesBoundary } from "@/components/DataStates";
import { TransactionFilters } from "./TransactionFilters";
import { Pagination } from "@/components/Pagination";
import { useTransactions, type TxnFilters } from "./api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Transaction, TxnStatus } from "@/types/api";

const TXN_STATUS: Record<TxnStatus, string> = { SETTLED: "Settled", PENDING: "Pending", REVERSED: "Reversed" };

function toParams(f: TxnFilters): URLSearchParams {
  const p = new URLSearchParams();
  if (f.page > 1) p.set("page", String(f.page));
  if (f.from) p.set("from", f.from);
  if (f.to) p.set("to", f.to);
  if (f.merchant) p.set("merchant", f.merchant);
  return p;
}

export function Component() {
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const filters: TxnFilters = {
    page: Number(sp.get("page") ?? 1),
    pageSize: 20,
    from: sp.get("from") ?? undefined,
    to: sp.get("to") ?? undefined,
    merchant: sp.get("merchant") ?? undefined,
  };

  const { data, isLoading, isError, isFetching, refetch } = useTransactions(filters);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Transactions</h1>
      <TransactionFilters value={filters} onChange={(next) => setSp(toParams(next))} />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your activity</CardTitle>
        </CardHeader>
        <CardContent>
          <DataStatesBoundary
            isLoading={isLoading}
            isError={isError}
            onRetry={refetch}
            isEmpty={!!data && data.items.length === 0}
            emptyTitle="No transactions match your filters."
          >
            <Table>
              <TableCaption className="sr-only">List of your transactions</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Merchant</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((t: Transaction) => (
                  <TableRow
                    key={t.id}
                    className="cursor-pointer"
                    onClick={() => navigate(`/transactions/${t.id}`)}
                  >
                    <TableCell>{formatDate(t.transactionDate)}</TableCell>
                    <TableCell>{t.merchantName}</TableCell>
                    <TableCell className="font-mono">
                      <Link
                        to={`/transactions/${t.id}`}
                        className="underline-offset-4 hover:underline focus-visible:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {t.reference}
                      </Link>
                    </TableCell>
                    <TableCell className="text-right">{formatCurrency(t.amount, t.currency)}</TableCell>
                    <TableCell>
                      <Badge variant="outline">{TXN_STATUS[t.status]}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </DataStatesBoundary>

          {data && (
            <Pagination
              page={filters.page}
              pageSize={filters.pageSize}
              total={data.total}
              isFetching={isFetching}
              onPageChange={(page) => setSp(toParams({ ...filters, page }))}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
