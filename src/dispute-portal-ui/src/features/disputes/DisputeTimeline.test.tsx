import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DisputeTimeline } from "./DisputeTimeline";
import type { DisputeEvent } from "@/types/api";

const events: DisputeEvent[] = [
  { eventType: "RESOLVED", description: "Closed", actorId: null, actorName: null, createdAt: "2026-07-16T10:00:00Z" },
  { eventType: "SUBMITTED", description: "Raised", actorId: null, actorName: null, createdAt: "2026-07-14T09:00:00Z" },
  { eventType: "UNDER_REVIEW", description: "Picked up", actorId: null, actorName: null, createdAt: "2026-07-15T11:00:00Z" },
];

describe("DisputeTimeline", () => {
  it("renders events in chronological order (oldest first) regardless of input order", () => {
    render(<DisputeTimeline events={events} />);
    const headings = screen.getAllByRole("heading", { level: 3 }).map((h) => h.textContent);
    expect(headings).toEqual(["Dispute submitted", "Under review", "Resolved"]);
  });

  it("renders a semantic ordered list with a time element per event", () => {
    render(<DisputeTimeline events={events} />);
    expect(screen.getByRole("list", { name: /dispute timeline/i })).toBeInTheDocument();
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
  });
});
