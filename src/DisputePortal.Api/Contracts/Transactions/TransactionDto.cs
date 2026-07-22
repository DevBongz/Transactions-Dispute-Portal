namespace DisputePortal.Api.Contracts.Transactions;

/// <summary>
/// Transaction projection returned to the customer (TDP-TXN-01 §2.3, SPEC §3.3).
/// <paramref name="HasDispute"/> is a convenience flag (true when a Dispute already
/// exists for this transaction) so the UI can disable the "Dispute this transaction"
/// action — reinforcing the duplicate-dispute guard in TDP-DISP-01.
/// </summary>
public sealed record TransactionDto(
    Guid Id,
    string Reference,
    string MerchantName,
    string? MerchantCategory,
    decimal Amount,
    string Currency,
    DateTimeOffset TransactionDate,
    string Status,
    bool HasDispute);
