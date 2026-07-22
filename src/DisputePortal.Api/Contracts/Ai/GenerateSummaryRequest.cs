using System.ComponentModel.DataAnnotations;

namespace DisputePortal.Api.Contracts.Ai;

/// <summary>
/// Body for <c>POST /ai/generate-summary</c> (TDP-AI-03 §2.1, SPEC §3.3). The analyst's
/// <paramref name="Outcome"/> and <paramref name="InternalNotes"/> for the dispute identified
/// by <paramref name="DisputeId"/>. Outcome is validated against the resolution enum and notes
/// must be ≥ 20 chars (AC-OPS-04) before any Anthropic call.
/// </summary>
public sealed record GenerateSummaryRequest(
    [property: Required] Guid DisputeId,
    [property: Required] string Outcome,
    [property: Required, MinLength(20)] string InternalNotes);
