import { formatDateTime } from "@/lib/format";
import type { DisputeEvent, DisputeEventType } from "@/types/api";

const EVENT_LABEL: Record<DisputeEventType, string> = {
  SUBMITTED: "Dispute submitted",
  CLASSIFIED: "Categorised",
  ASSIGNED: "Assigned to an analyst",
  UNDER_REVIEW: "Under review",
  RESOLVED: "Resolved",
  REOPENED: "Reopened",
};

/**
 * Chronological (oldest → newest) vertical timeline (TDP-FE-04 §2.6, TRACK-02). Sorted
 * client-side defensively; rendered as a semantic <ol> so order and count are announced.
 */
export function DisputeTimeline({ events }: { events: DisputeEvent[] }) {
  const ordered = [...events].sort((a, b) => +new Date(a.createdAt) - +new Date(b.createdAt));

  if (ordered.length === 0) {
    return <p className="text-sm text-muted-foreground">No events yet.</p>;
  }

  return (
    <ol className="relative ml-2 border-l pl-6" aria-label="Dispute timeline">
      {ordered.map((e, i) => (
        <li
          key={`${e.eventType}-${e.createdAt}-${i}`}
          className="relative mb-6 last:mb-0"
          data-testid="timeline-item"
          data-event-type={e.eventType}
        >
          <span className="absolute -left-[1.9rem] top-1 h-3 w-3 rounded-full bg-primary" aria-hidden />
          <h3 className="font-medium">{EVENT_LABEL[e.eventType] ?? e.eventType}</h3>
          <time dateTime={e.createdAt} className="text-sm text-muted-foreground">
            {formatDateTime(e.createdAt)}
          </time>
          {e.description && <p className="mt-0.5 text-sm">{e.description}</p>}
        </li>
      ))}
    </ol>
  );
}
