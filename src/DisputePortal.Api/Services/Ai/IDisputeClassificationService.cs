using DisputePortal.Api.Domain;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Classifies a submitted dispute into a category + priority via Claude (TDP-AI-02 §2.4/§2.5).
/// The service never throws for AI failures: it returns a <see cref="ClassificationResult"/>
/// with <see cref="ClassificationResult.Success"/> = false so the consumer can apply the
/// non-blocking <c>CLASSIFICATION_FAILED</c> fallback (AC-AI-02) and still commit the offset.
/// </summary>
public interface IDisputeClassificationService
{
    Task<ClassificationResult> ClassifyAsync(ClassificationContext context, CancellationToken ct);
}

/// <summary>
/// The facts injected into the classification prompt (SPEC §3.5 Feature 2 "Context injected
/// into user message"). Assembled by the consumer from the persisted dispute + transaction.
/// </summary>
public sealed record ClassificationContext(
    string MerchantName,
    string? MerchantCategory,
    decimal Amount,
    DateTimeOffset TransactionDate,
    string CustomerDescription,
    int CustomerOpenDisputeCount);

/// <summary>
/// Outcome of a classification attempt. On success carries the validated category/priority
/// and the model's one-sentence rationale; on failure carries a reason for the audit trail.
/// </summary>
public sealed record ClassificationResult(
    bool Success,
    DisputeCategory? Category,
    DisputePriority? Priority,
    string? Rationale,
    string? FailureReason)
{
    public static ClassificationResult Ok(DisputeCategory category, DisputePriority priority, string? rationale) =>
        new(true, category, priority, rationale, null);

    public static ClassificationResult Failed(string reason) =>
        new(false, null, null, null, reason);
}
