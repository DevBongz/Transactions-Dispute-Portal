import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { DataStatesBoundary } from "@/components/DataStates";
import { Pagination } from "@/components/Pagination";
import { StatusBadge } from "@/components/StatusBadge";
import { useMyDisputes, type MyDisputesFilters } from "./history-api";
import { CATEGORY_LABEL } from "@/lib/labels";
import { formatDate } from "@/lib/format";
import type { Dispute, DisputeStatus } from "@/types/api";

const STATUS_OPTIONS: { value: DisputeStatus | ""; label: string }[] = [
  { value: "", label: "All statuses" },
  { value: "OPEN", label: "Open" },
  { value: "UNDER_REVIEW", label: "Under Review" },
  { value: "RESOLVED", label: "Resolved" },
];

export function Component() {
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const filters: MyDisputesFilters = {
    page: Number(sp.get("page") ?? 1),
    pageSize: 20,
    status: (sp.get("status") as DisputeStatus | null) ?? undefined,
  };
  const { data, isLoading, isError, isFetching, refetch } = useMyDisputes(filters);

  const setParams = (next: Partial<MyDisputesFilters>) => {
    const merged = { ...filters, ...next };
    const p = new URLSearchParams();
    if (merged.page > 1) p.set("page", String(merged.page));
    if (merged.status) p.set("status", merged.status);
    setSp(p);
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">My Disputes</h1>

      <div className="flex items-end gap-4 rounded-lg border p-4">
        <div className="space-y-2">
          <Label htmlFor="status">Status</Label>
          <Select
            id="status"
            className="w-[12rem]"
            value={filters.status ?? ""}
            onChange={(e) => setParams({ page: 1, status: (e.target.value || undefined) as DisputeStatus | undefined })}
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your disputes</CardTitle>
        </CardHeader>
        <CardContent>
          <DataStatesBoundary
            isLoading={isLoading}
            isError={isError}
            onRetry={refetch}
            isEmpty={!!data && data.items.length === 0}
            emptyTitle="You haven't raised any disputes yet."
            emptyChildren={<Link to="/transactions" className="underline">Go to transactions</Link>}
          >
            <Table>
              <TableCaption className="sr-only">List of your disputes</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((d: Dispute) => (
                  <TableRow key={d.id} className="cursor-pointer" onClick={() => navigate(`/my-disputes/${d.id}`)}>
                    <TableCell className="font-mono">
                      <Link
                        to={`/my-disputes/${d.id}`}
                        className="underline-offset-4 hover:underline focus-visible:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {d.reference}
                      </Link>
                    </TableCell>
                    <TableCell>{formatDate(d.createdAt)}</TableCell>
                    <TableCell>
                      {d.category ? (
                        CATEGORY_LABEL[d.category]
                      ) : (
                        <span className="text-muted-foreground">
                          <Badge variant="outline">Pending classification</Badge>
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={d.status} customerView />
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
              onPageChange={(page) => setParams({ page })}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
