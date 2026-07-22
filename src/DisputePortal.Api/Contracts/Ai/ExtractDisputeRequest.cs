using System.ComponentModel.DataAnnotations;

namespace DisputePortal.Api.Contracts.Ai;

/// <summary>
/// Body for <c>POST /ai/extract-dispute</c> (TDP-AI-01 §2.1, SPEC §3.3). The single free-text
/// customer description; validated for presence and length before any Anthropic call.
/// </summary>
public sealed record ExtractDisputeRequest(
    [Required] string Text);
