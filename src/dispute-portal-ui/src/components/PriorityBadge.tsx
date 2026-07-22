import { Badge, type BadgeProps } from "@/components/ui/badge";
import type { Priority } from "@/types/api";

const PRIORITY: Record<Priority, { label: string; variant: BadgeProps["variant"] }> = {
  CRITICAL: { label: "Critical", variant: "destructive" },
  HIGH: { label: "High", variant: "default" },
  MEDIUM: { label: "Medium", variant: "secondary" },
  LOW: { label: "Low", variant: "outline" },
};

/** Shared priority badge (TDP-FE-05 §2.3). Conveyed by text + colour. Null → "Pending". */
export function PriorityBadge({ priority }: { priority: Priority | null }) {
  if (!priority) {
    return (
      <Badge variant="outline" aria-label="Priority: Pending">
        Pending
      </Badge>
    );
  }
  const p = PRIORITY[priority];
  return (
    <Badge variant={p.variant} aria-label={`Priority: ${p.label}`}>
      {p.label}
    </Badge>
  );
}
