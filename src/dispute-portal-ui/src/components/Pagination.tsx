import { Button } from "@/components/ui/button";

/** Minimal prev/next pager driven by total/page/pageSize. */
export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  isFetching,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  isFetching?: boolean;
}) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <div className="flex flex-col gap-3 pt-4 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-sm text-muted-foreground" aria-live="polite">
        {total === 0 ? "No results" : `Showing ${from}–${to} of ${total}`}
        {isFetching && " · updating…"}
      </p>
      <div className="flex items-center justify-between gap-2 sm:justify-end">
        <Button
          variant="outline"
          size="sm"
          className="flex-1 sm:flex-none"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
        >
          Previous
        </Button>
        <span className="shrink-0 text-sm" aria-current="page">
          Page {page} of {totalPages}
        </span>
        <Button
          variant="outline"
          size="sm"
          className="flex-1 sm:flex-none"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
