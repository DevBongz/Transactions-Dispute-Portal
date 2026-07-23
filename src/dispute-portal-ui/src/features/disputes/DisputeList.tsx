import { StatusBadge } from "@/components/StatusBadge";
import { CATEGORY_LABEL } from "@/lib/labels";
import { formatDate } from "@/lib/format";
import type { Dispute } from "@/types/api";

/**
 * Presentational dispute table shared by customer history tests (TDP-TEST-02 DisputeList).
 * MyDisputesPage keeps its own table wired to React Query; this component locks badge /
 * empty-state behaviour under Vitest.
 */
export function DisputeList({
  disputes,
  customerView = true,
}: {
  disputes: Dispute[];
  customerView?: boolean;
}) {
  if (disputes.length === 0) {
    return <p>No disputes yet.</p>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Reference</th>
          <th>Submitted</th>
          <th>Category</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {disputes.map((d) => (
          <tr key={d.id}>
            <td>{d.reference}</td>
            <td>{formatDate(d.createdAt)}</td>
            <td>{d.category ? CATEGORY_LABEL[d.category] : "—"}</td>
            <td>
              <StatusBadge status={d.status} customerView={customerView} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
