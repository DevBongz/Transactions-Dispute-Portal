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
    const order = screen.getAllByTestId("timeline-item").map((el) => el.getAttribute("data-event-type"));
    expect(order).toEqual(["SUBMITTED", "UNDER_REVIEW", "RESOLVED"]);
  });

  it("renders a semantic ordered list with a time element per event", () => {
    render(<DisputeTimeline events={events} />);
    expect(screen.getByRole("list", { name: /dispute timeline/i })).toBeInTheDocument();
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
  });

  it("renders a human-readable description for each event", () => {
    render(<DisputeTimeline events={events} />);
    expect(screen.getByText(/raised/i)).toBeInTheDocument();
    expect(screen.getByText(/closed/i)).toBeInTheDocument();
  });
});
