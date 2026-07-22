using System.ComponentModel.DataAnnotations;

namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Body for <c>POST /disputes/{id}/resolve</c> (TDP-DISP-03 §2.2). <paramref name="Outcome"/>
/// must be a valid <see cref="Domain.ResolutionOutcome"/>; <paramref name="InternalNotes"/>
/// must be ≥ 20 chars (AC-OPS-04); <paramref name="CustomerSummary"/> is optional (AI-generated).
/// </summary>
public sealed record ResolveDisputeRequest(
    [Required] string Outcome,
    [Required, MinLength(20)] string InternalNotes,
    string? CustomerSummary);
