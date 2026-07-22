namespace DisputePortal.Api.Domain;

public sealed class Dispute
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = default!;        // DSP-YYYYMMDD-NNNNN
    public Guid TransactionId { get; set; }
    public Guid CustomerId { get; set; }
    public DisputeStatus Status { get; set; }
    public DisputeCategory? Category { get; set; }           // null until classified
    public DisputePriority? Priority { get; set; }           // null until classified
    public string CustomerDescription { get; set; } = default!;
    public string? ExtractedFieldsJson { get; set; }         // JSONB
    public Guid? AssignedToId { get; set; }                  // ops analyst, nullable
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Transaction Transaction { get; set; } = default!;
    public User Customer { get; set; } = default!;
    public User? AssignedTo { get; set; }
    public ICollection<DisputeEvent> Events { get; set; } = new List<DisputeEvent>();
    public Resolution? Resolution { get; set; }              // zero or one
}
