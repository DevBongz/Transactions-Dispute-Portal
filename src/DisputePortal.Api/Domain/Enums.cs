namespace DisputePortal.Api.Domain;

public enum UserRole { CUSTOMER, OPS_ANALYST, OPS_MANAGER }

public enum TransactionStatus { SETTLED, PENDING, REVERSED }

public enum DisputeStatus { OPEN, UNDER_REVIEW, RESOLVED, CLASSIFICATION_FAILED }

public enum DisputeCategory { UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER }

public enum DisputePriority { LOW, MEDIUM, HIGH, CRITICAL }

public enum DisputeEventType { SUBMITTED, CLASSIFIED, ASSIGNED, UNDER_REVIEW, RESOLVED }

public enum ResolutionOutcome { UPHELD, DECLINED, PARTIAL }
