import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatusBadge } from "./StatusBadge";

describe("StatusBadge", () => {
  it("renders distinct label text for each status", () => {
    render(<StatusBadge status="OPEN" />);
    expect(screen.getByText("Open")).toBeInTheDocument();
  });

  it("maps CLASSIFICATION_FAILED to 'Under Review' in the customer view", () => {
    render(<StatusBadge status="CLASSIFICATION_FAILED" customerView />);
    expect(screen.getByText("Under Review")).toBeInTheDocument();
    expect(screen.queryByText("Needs Triage")).not.toBeInTheDocument();
  });

  it("shows the raw internal state to ops", () => {
    render(<StatusBadge status="CLASSIFICATION_FAILED" />);
    expect(screen.getByText("Needs Triage")).toBeInTheDocument();
  });
});
