using DisputePortal.Api.Contracts.Ai;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Generates a plain-language, customer-facing resolution summary from an analyst's internal
/// notes (TDP-AI-03). Pure generation — no DB writes, no Kafka publish; persistence happens
/// at resolution time (TDP-DISP-03).
/// </summary>
public interface IResolutionSummaryService
{
    /// <summary>
    /// Generate a customer summary for the dispute in <paramref name="request"/>.
    /// </summary>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">The dispute does not exist.</exception>
    /// <exception cref="Infrastructure.Exceptions.ValidationException">Invalid outcome / too-short notes.</exception>
    /// <exception cref="AnthropicException">Upstream failure/timeout or empty output.</exception>
    Task<GenerateSummaryResponse> GenerateAsync(GenerateSummaryRequest request, CancellationToken ct);
}
