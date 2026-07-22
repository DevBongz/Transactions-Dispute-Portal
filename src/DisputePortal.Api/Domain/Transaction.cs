namespace DisputePortal.Api.Domain;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Reference { get; set; } = default!;        // TXN-YYYYMMDD-NNNNN
    public string MerchantName { get; set; } = default!;
    public string? MerchantCategory { get; set; }            // MCC description
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";            // ISO 4217, CHAR(3)
    public DateTimeOffset TransactionDate { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User Customer { get; set; } = default!;
    public Dispute? Dispute { get; set; }                    // zero or one
}
