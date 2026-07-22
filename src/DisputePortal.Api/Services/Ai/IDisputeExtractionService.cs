using DisputePortal.Api.Contracts.Ai;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Turns a customer's free-text description into structured, pre-populated dispute fields
/// plus a per-field confidence map (TDP-AI-01). Read-only — no persistence; the customer
/// reviews/edits and then submits via <c>POST /disputes</c>.
/// </summary>
public interface IDisputeExtractionService
{
    /// <summary>
    /// Extract dispute fields from <paramref name="text"/>.
    /// </summary>
    /// <exception cref="AnthropicException">Upstream failure/timeout or unparseable model output.</exception>
    Task<ExtractDisputeResponse> ExtractAsync(string text, CancellationToken ct);
}
