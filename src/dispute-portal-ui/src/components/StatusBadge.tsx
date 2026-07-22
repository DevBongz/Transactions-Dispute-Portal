import { Badge, type BadgeProps } from "@/components/ui/badge";
import type { DisputeStatus } from "@/types/api";

const STATUS: Record<DisputeStatus, { label: string; variant: BadgeProps["variant"] }> = {
  OPEN: { label: "Open", variant: "secondary" },
  UNDER_REVIEW: { label: "Under Review", variant: "default" },
  RESOLVED: { label: "Resolved", variant: "outline" },
  CLASSIFICATION_FAILED: { label: "Needs Triage", variant: "destructive" },
};

/**
 * Shared dispute status badge (TDP-FE-04 §2.3). Status is conveyed by label text as well as
 * colour (never colour alone, WCAG). For customers, the internal `CLASSIFICATION_FAILED` state
 * is displayed as "Under Review" so a triage failure never surfaces as alarming; ops see the
 * raw value (`customerView={false}`, the default).
 */
export function StatusBadge({
  status,
  customerView = false,
}: {
  status: DisputeStatus;
  customerView?: boolean;
}) {
  const effective =
    customerView && status === "CLASSIFICATION_FAILED" ? "UNDER_REVIEW" : status;
  const s = STATUS[effective];
  return (
    <Badge variant={s.variant} aria-label={`Status: ${s.label}`}>
      {s.label}
    </Badge>
  );
}
