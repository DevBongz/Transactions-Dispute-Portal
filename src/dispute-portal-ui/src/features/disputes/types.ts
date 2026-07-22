import type { DisputeCategory } from "@/types/api";

/** Matches the backend contract (SPEC §3.5). Centralised so it stays aligned. */
export const CONFIDENCE_THRESHOLD = 0.6;

export interface DisputeFormValues {
  category: DisputeCategory | "";
  amount: string;
  merchantName: string;
  transactionDate: string; // yyyy-MM-dd
  description: string;
}

export type DisputeFormField = keyof DisputeFormValues;

export const EMPTY_FORM: DisputeFormValues = {
  category: "",
  amount: "",
  merchantName: "",
  transactionDate: "",
  description: "",
};
