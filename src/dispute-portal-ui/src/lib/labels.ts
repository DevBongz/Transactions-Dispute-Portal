import type { DisputeCategory, Outcome, Priority } from "@/types/api";

/** Human-readable labels for the dispute category enum (submit the enum, show the label). */
export const CATEGORY_LABEL: Record<DisputeCategory, string> = {
  UNAUTHORISED: "Unauthorised",
  DUPLICATE_CHARGE: "Duplicate charge",
  MERCHANT_ERROR: "Merchant error",
  WRONG_AMOUNT: "Wrong amount",
  OTHER: "Other",
};

export const CATEGORY_OPTIONS = Object.entries(CATEGORY_LABEL).map(([value, label]) => ({
  value: value as DisputeCategory,
  label,
}));

export const PRIORITY_LABEL: Record<Priority, string> = {
  CRITICAL: "Critical",
  HIGH: "High",
  MEDIUM: "Medium",
  LOW: "Low",
};

/** Customer-facing outcome phrasing (TDP-FE-04 §2.7). */
export const OUTCOME_LABEL: Record<Outcome, string> = {
  UPHELD: "Resolved in your favour",
  DECLINED: "Not upheld",
  PARTIAL: "Partially upheld",
};

/** Neutral outcome labels for ops-facing controls. */
export const OUTCOME_OPS_LABEL: Record<Outcome, string> = {
  UPHELD: "Upheld",
  DECLINED: "Declined",
  PARTIAL: "Partial",
};
