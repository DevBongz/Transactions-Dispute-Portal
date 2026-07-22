import type { ReactNode } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState } from "@/components/DataStates";
import { useDashboardSummary } from "./api";
import { CATEGORY_LABEL, PRIORITY_LABEL } from "@/lib/labels";
import type { DisputeCategory, Priority } from "@/types/api";

const PRIORITY_ORDER: Priority[] = ["CRITICAL", "HIGH", "MEDIUM", "LOW"];
const CATEGORY_ORDER: DisputeCategory[] = [
  "UNAUTHORISED",
  "DUPLICATE_CHARGE",
  "MERCHANT_ERROR",
  "WRONG_AMOUNT",
  "OTHER",
];

function StatCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

/** Manager metrics strip (TDP-FE-05 §2.7, OPS-06). Refreshed on page load; text + values only. */
export function DashboardMetrics() {
  const { data, isLoading, isError, refetch } = useDashboardSummary();

  if (isError) return <ErrorState onRetry={refetch} message="Couldn't load dashboard metrics." />;

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
      <StatCard title="Total open">
        {isLoading ? <Skeleton className="h-8 w-16" /> : <p className="text-3xl font-semibold">{data!.totalOpen}</p>}
      </StatCard>

      <StatCard title="By priority">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : (
          <ul className="space-y-1 text-sm">
            {PRIORITY_ORDER.map((p) => (
              <li key={p} className="flex justify-between">
                <span>{PRIORITY_LABEL[p]}</span>
                <span className="font-medium">{data!.byPriority[p] ?? 0}</span>
              </li>
            ))}
          </ul>
        )}
      </StatCard>

      <StatCard title="By category">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : (
          <ul className="space-y-1 text-sm">
            {CATEGORY_ORDER.map((c) => (
              <li key={c} className="flex justify-between gap-2">
                <span className="truncate">{CATEGORY_LABEL[c]}</span>
                <span className="font-medium">{data!.byCategory[c] ?? 0}</span>
              </li>
            ))}
          </ul>
        )}
      </StatCard>

      <StatCard title="Avg resolution (30d)">
        {isLoading ? (
          <Skeleton className="h-8 w-24" />
        ) : (
          <p className="text-3xl font-semibold">
            {data!.avgResolutionHours.toFixed(1)} <span className="text-base font-normal text-muted-foreground">hrs</span>
          </p>
        )}
      </StatCard>
    </div>
  );
}
