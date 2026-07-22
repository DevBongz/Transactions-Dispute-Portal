import { describe, expect, it } from "vitest";
import { byPriorityDesc } from "./api";
import type { Dispute } from "@/types/api";

function d(priority: Dispute["priority"], createdAt: string): Dispute {
  return {
    id: createdAt,
    reference: "DSP",
    transactionId: "t",
    customerId: "c",
    customerName: null,
    status: "OPEN",
    category: null,
    priority,
    createdAt,
    updatedAt: createdAt,
  };
}

describe("byPriorityDesc", () => {
  it("sorts CRITICAL > HIGH > MEDIUM > LOW", () => {
    const rows = [d("LOW", "2026-01-01"), d("CRITICAL", "2026-01-02"), d("MEDIUM", "2026-01-03"), d("HIGH", "2026-01-04")];
    expect(rows.sort(byPriorityDesc).map((r) => r.priority)).toEqual(["CRITICAL", "HIGH", "MEDIUM", "LOW"]);
  });

  it("breaks ties by oldest-first within a priority tier", () => {
    const rows = [d("HIGH", "2026-02-02"), d("HIGH", "2026-02-01")];
    expect(rows.sort(byPriorityDesc).map((r) => r.createdAt)).toEqual(["2026-02-01", "2026-02-02"]);
  });

  it("treats a null priority as LOW", () => {
    const rows = [d(null, "2026-03-01"), d("MEDIUM", "2026-03-02")];
    expect(rows.sort(byPriorityDesc).map((r) => r.priority)).toEqual(["MEDIUM", null]);
  });
});
