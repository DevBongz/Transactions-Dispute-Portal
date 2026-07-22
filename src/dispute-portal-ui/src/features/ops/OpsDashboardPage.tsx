import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { DataStatesBoundary } from "@/components/DataStates";
import { Pagination } from "@/components/Pagination";
import { StatusBadge } from "@/components/StatusBadge";
import { PriorityBadge } from "@/components/PriorityBadge";
import { DashboardMetrics } from "./DashboardMetrics";
import { byPriorityDesc, useOpsDisputes, type OpsFilters } from "./api";
import { CATEGORY_LABEL, CATEGORY_OPTIONS, PRIORITY_LABEL } from "@/lib/labels";
import { formatDate } from "@/lib/format";
import type { DisputeCategory, DisputeStatus, Priority } from "@/types/api";

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "All statuses" },
  { value: "OPEN", label: "Open" },
  { value: "UNDER_REVIEW", label: "Under Review" },
  { value: "RESOLVED", label: "Resolved" },
  { value: "CLASSIFICATION_FAILED", label: "Needs Triage" },
];
const PRIORITY_OPTIONS: Priority[] = ["CRITICAL", "HIGH", "MEDIUM", "LOW"];

export function Component() {
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const filters: OpsFilters = {
    page: Number(sp.get("page") ?? 1),
    pageSize: 20,
    status: (sp.get("status") as DisputeStatus | null) ?? undefined,
    priority: (sp.get("priority") as Priority | null) ?? undefined,
    category: (sp.get("category") as DisputeCategory | null) ?? undefined,
  };
  const { data, isLoading, isError, isFetching, refetch } = useOpsDisputes(filters);

  const setParams = (next: Partial<OpsFilters>) => {
    const merged = { ...filters, ...next };
    const p = new URLSearchParams();
    if (merged.page > 1) p.set("page", String(merged.page));
    if (merged.status) p.set("status", merged.status);
    if (merged.priority) p.set("priority", merged.priority);
    if (merged.category) p.set("category", merged.category);
    setSp(p);
  };

  const rows = data ? [...data.items].sort(byPriorityDesc) : [];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Operations</h1>

      <DashboardMetrics />

      <div className="flex flex-wrap items-end gap-4 rounded-lg border p-4">
        <div className="space-y-2">
          <Label htmlFor="f-status">Status</Label>
          <Select
            id="f-status"
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
        <div className="space-y-2">
          <Label htmlFor="f-priority">Priority</Label>
          <Select
            id="f-priority"
            className="w-[10rem]"
            value={filters.priority ?? ""}
            onChange={(e) => setParams({ page: 1, priority: (e.target.value || undefined) as Priority | undefined })}
          >
            <option value="">All priorities</option>
            {PRIORITY_OPTIONS.map((p) => (
              <option key={p} value={p}>
                {PRIORITY_LABEL[p]}
              </option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="f-category">Category</Label>
          <Select
            id="f-category"
            className="w-[14rem]"
            value={filters.category ?? ""}
            onChange={(e) => setParams({ page: 1, category: (e.target.value || undefined) as DisputeCategory | undefined })}
          >
            <option value="">All categories</option>
            {CATEGORY_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dispute queue</CardTitle>
        </CardHeader>
        <CardContent>
          <DataStatesBoundary
            isLoading={isLoading}
            isError={isError}
            onRetry={refetch}
            isEmpty={!!data && data.items.length === 0}
            emptyTitle="No disputes match these filters."
            skeletonCols={7}
          >
            <Table>
              <TableCaption className="sr-only">Dispute queue sorted by priority</TableCaption>
              <TableHeader>
                <TableRow>
                  <TableHead>Priority</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Submitted</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => (
                  <TableRow key={d.id} className="cursor-pointer" onClick={() => navigate(`/ops/disputes/${d.id}`)}>
                    <TableCell>
                      <PriorityBadge priority={d.priority} />
                    </TableCell>
                    <TableCell className="font-mono">
                      <Link
                        to={`/ops/disputes/${d.id}`}
                        className="underline-offset-4 hover:underline focus-visible:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {d.reference}
                      </Link>
                    </TableCell>
                    <TableCell>{d.customerName ?? "—"}</TableCell>
                    <TableCell>
                      {d.category ? CATEGORY_LABEL[d.category] : <Badge variant="outline">Pending</Badge>}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={d.status} />
                    </TableCell>
                    <TableCell>{formatDate(d.createdAt)}</TableCell>
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
