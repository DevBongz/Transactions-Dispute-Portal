namespace DisputePortal.Api.Domain;

public enum UserRole { CUSTOMER, OPS_ANALYST, OPS_MANAGER }

public enum TransactionStatus { SETTLED, PENDING, REVERSED }

public enum DisputeStatus { OPEN, UNDER_REVIEW, RESOLVED, CLASSIFICATION_FAILED }

public enum DisputeCategory { UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER }

public enum DisputePriority { LOW, MEDIUM, HIGH, CRITICAL }

// SPEC §3.2 core set (SUBMITTED, CLASSIFIED, ASSIGNED, UNDER_REVIEW, RESOLVED) plus REOPENED,
// recorded when ops moves a dispute back to OPEN (TDP-DISP-02 §2.5 re-open). Stored as a string
// in a VARCHAR(100) column, so the extra value needs no schema change / migration.
public enum DisputeEventType { SUBMITTED, CLASSIFIED, ASSIGNED, UNDER_REVIEW, RESOLVED, REOPENED }

public enum ResolutionOutcome { UPHELD, DECLINED, PARTIAL }
