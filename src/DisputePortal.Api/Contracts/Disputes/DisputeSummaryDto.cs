namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Row projection for the dispute list (TDP-DISP-02 §2.2). <paramref name="CustomerName"/>
/// is populated only for ops callers; customers never see other identities.
/// </summary>
public sealed record DisputeSummaryDto(
    Guid Id,
    string Reference,
    Guid TransactionId,
    Guid CustomerId,
    string? CustomerName,
    string Status,
    string? Category,
    string? Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
