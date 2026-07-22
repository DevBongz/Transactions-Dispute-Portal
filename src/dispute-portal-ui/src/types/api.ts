// Shared DTO contracts mirroring SPEC §3.2/§3.3 — one source of truth for all features.

export type Role = "CUSTOMER" | "OPS_ANALYST" | "OPS_MANAGER";

export interface AuthUser {
  id: string;
  fullName: string;
  role: Role;
}
export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

export type TxnStatus = "SETTLED" | "PENDING" | "REVERSED";
export interface Transaction {
  id: string;
  reference: string;
  merchantName: string;
  merchantCategory: string | null;
  amount: number;
  currency: string;
  transactionDate: string;
  status: TxnStatus;
  hasDispute?: boolean;
}

export type DisputeStatus = "OPEN" | "UNDER_REVIEW" | "RESOLVED" | "CLASSIFICATION_FAILED";
export type DisputeCategory =
  | "UNAUTHORISED"
  | "DUPLICATE_CHARGE"
  | "MERCHANT_ERROR"
  | "WRONG_AMOUNT"
  | "OTHER";
export type Priority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Dispute list row (SPEC §3.3, DisputeSummaryDto). */
export interface Dispute {
  id: string;
  reference: string;
  transactionId: string;
  customerId: string;
  customerName: string | null;
  status: DisputeStatus;
  category: DisputeCategory | null;
  priority: Priority | null;
  createdAt: string;
  updatedAt: string;
}

export type DisputeEventType =
  | "SUBMITTED"
  | "CLASSIFIED"
  | "ASSIGNED"
  | "UNDER_REVIEW"
  | "RESOLVED"
  | "REOPENED";

export interface DisputeEvent {
  eventType: DisputeEventType;
  description: string | null;
  actorId: string | null;
  actorName: string | null;
  createdAt: string;
}

export type Outcome = "UPHELD" | "DECLINED" | "PARTIAL";
export interface Resolution {
  outcome: Outcome;
  customerSummary: string | null;
  resolvedById: string;
  resolvedAt: string;
}

/** Full dispute detail (SPEC §3.3, DisputeDetailDto). */
export interface DisputeDetail {
  id: string;
  reference: string;
  status: DisputeStatus;
  category: DisputeCategory | null;
  priority: Priority | null;
  customerDescription: string;
  extractedFields: Record<string, unknown> | null;
  assignedToId: string | null;
  customerId: string;
  customerName: string;
  customerEmail: string;
  transaction: Transaction;
  resolution: Resolution | null;
  timeline: DisputeEvent[];
}

/** AI extraction response (SPEC §3.3, TDP-AI-01). */
export interface ExtractDisputeResponse {
  transactionRef?: string | null;
  category?: DisputeCategory | null;
  amount?: number | null;
  merchantName?: string | null;
  transactionDate?: string | null;
  confidence: Partial<
    Record<"transactionRef" | "category" | "amount" | "merchantName" | "transactionDate", number>
  >;
}

/** Ops dashboard summary (SPEC §3.3, OPS-06). */
export interface DashboardSummary {
  totalOpen: number;
  byPriority: Record<Priority, number>;
  byCategory: Record<DisputeCategory, number>;
  avgResolutionHours: number;
}
