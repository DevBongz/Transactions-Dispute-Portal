using DisputePortal.Api.Contracts.Ai;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Controllers;

/// <summary>
/// AI-assist endpoints (TDP-AI-01 / TDP-AI-03, SPEC §3.3). Extraction is available to any
/// authenticated user (primarily customers); summary generation is ops-only. Both are pure
/// assist calls — no persistence here. A Gemini failure/timeout surfaces as <c>502</c>
/// with a generic body so the caller can fall back to manual entry (the API key is never
/// echoed or logged, SPEC §3.6).
/// </summary>
[ApiController]
[Route("api/v1/ai")]
[Authorize]
[Produces("application/json")]
public sealed class AiController(
    IDisputeExtractionService extraction,
    IResolutionSummaryService summary,
    IOptions<GeminiOptions> options) : ControllerBase
{
    private readonly GeminiOptions _opts = options.Value;

    /// <summary>Extract structured dispute fields from a free-text description (TDP-AI-01).</summary>
    [HttpPost("extract-dispute")]
    [ProducesResponseType(typeof(ExtractDisputeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ExtractDispute(
        [FromBody] ExtractDisputeRequest request, CancellationToken ct)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new ValidationException("Text is required.");
        if (text.Length > _opts.MaxExtractionInputChars)
            throw new ValidationException($"Text must be at most {_opts.MaxExtractionInputChars} characters.");

        try
        {
            var result = await extraction.ExtractAsync(text, ct);
            return Ok(result);
        }
        catch (AnthropicException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "extraction_unavailable" });
        }
    }

    /// <summary>Generate a customer-facing resolution summary from internal notes (TDP-AI-03, ops only).</summary>
    [HttpPost("generate-summary")]
    [Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
    [ProducesResponseType(typeof(GenerateSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GenerateSummary(
        [FromBody] GenerateSummaryRequest request, CancellationToken ct)
    {
        try
        {
            var result = await summary.GenerateAsync(request, ct);
            return Ok(result);
        }
        catch (AnthropicException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "summary_unavailable" });
        }
    }
}
