namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Configuration for the Google Gemini integration, bound from the <c>Gemini</c> config section.
/// <c>ApiKey</c> is injected server-side only from <c>GEMINI_API_KEY</c> / <c>Gemini__ApiKey</c>
/// and must never reach the frontend, a response body, or a log line.
/// </summary>
public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    // Feature 1 — natural-language extraction. Flash for low latency; 8s ceiling (free-tier
    // Gemini can be slower than paid Claude Haiku).
    public string ExtractionModel { get; set; } = "gemini-2.0-flash";
    public int ExtractionMaxTokens { get; set; } = 1024;
    public int ExtractionTimeoutSeconds { get; set; } = 8;

    // Feature 2 — background classification.
    public string ClassificationModel { get; set; } = "gemini-2.0-flash";
    public int ClassificationMaxTokens { get; set; } = 512;
    public int ClassificationTimeoutSeconds { get; set; } = 8;

    // Feature 3 — customer-facing resolution summary.
    public string SummaryModel { get; set; } = "gemini-2.0-flash";
    public int SummaryMaxTokens { get; set; } = 512;
    public int SummaryTimeoutSeconds { get; set; } = 15;

    // Guardrail on extraction input size — reject oversized text with 400 before any LLM call.
    public int MaxExtractionInputChars { get; set; } = 4000;
}
