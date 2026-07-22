import type { ReactNode } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

/** Row skeletons for a loading table. */
export function TableSkeleton({ rows = 8, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className="space-y-2" aria-busy>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex gap-4">
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} className="h-8 flex-1" />
          ))}
        </div>
      ))}
    </div>
  );
}

/** Retryable error alert used across list/detail pages. */
export function ErrorState({ message, onRetry }: { message?: string; onRetry?: () => void }) {
  return (
    <Alert variant="destructive">
      <AlertTitle>Something went wrong</AlertTitle>
      <AlertDescription className="flex items-center justify-between gap-4">
        <span>{message ?? "We couldn't load this. Please try again."}</span>
        {onRetry && (
          <Button size="sm" variant="outline" onClick={onRetry}>
            Retry
          </Button>
        )}
      </AlertDescription>
    </Alert>
  );
}

/** Neutral empty-result state with an optional call to action. */
export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed p-10 text-center">
      <p className="font-medium">{title}</p>
      {children && <div className="mt-2 text-sm text-muted-foreground">{children}</div>}
    </div>
  );
}

/**
 * Convenience wrapper that renders the loading / error / empty states and only shows children
 * once data is present. Keeps every list page consistent.
 */
export function DataStatesBoundary({
  isLoading,
  isError,
  onRetry,
  isEmpty,
  emptyTitle,
  emptyChildren,
  skeletonRows,
  skeletonCols,
  children,
}: {
  isLoading: boolean;
  isError: boolean;
  onRetry?: () => void;
  isEmpty?: boolean;
  emptyTitle?: string;
  emptyChildren?: ReactNode;
  skeletonRows?: number;
  skeletonCols?: number;
  children: ReactNode;
}) {
  if (isLoading) return <TableSkeleton rows={skeletonRows} cols={skeletonCols} />;
  if (isError) return <ErrorState onRetry={onRetry} />;
  if (isEmpty) return <EmptyState title={emptyTitle ?? "Nothing to show yet."}>{emptyChildren}</EmptyState>;
  return <>{children}</>;
}
