namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Configuration for the Anthropic Claude integration (TDP-AI-01/02/03), bound from the
/// <c>Anthropic</c> config section. <c>ApiKey</c> is injected server-side only from
/// <c>ANTHROPIC_API_KEY</c> (SPEC §3.1/§3.6) and must never reach the frontend, a response
/// body, or a log line. Model ids are pinned verbatim per SPEC §3.5 / §4.2 — do not shorten
/// them or change the date suffix.
/// </summary>
public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string AnthropicVersion { get; set; } = "2023-06-01";

    // Feature 1 — natural-language extraction (SPEC §3.5). Haiku for low latency; 5s ceiling
    // enforces AC-DISP-02 / the NFR "AI extraction endpoint response time < 5 seconds".
    public string ExtractionModel { get; set; } = "claude-haiku-4-5-20251001";
    public int ExtractionMaxTokens { get; set; } = 1024;
    public int ExtractionTimeoutSeconds { get; set; } = 5;

    // Feature 2 — background classification (SPEC §3.5). Small JSON, so fewer tokens; 5s
    // budget per AC-AI-02 "< 5 seconds after Kafka event consumed".
    public string ClassificationModel { get; set; } = "claude-haiku-4-5-20251001";
    public int ClassificationMaxTokens { get; set; } = 512;
    public int ClassificationTimeoutSeconds { get; set; } = 5;

    // Feature 3 — customer-facing resolution summary (SPEC §3.5). Sonnet for higher quality;
    // this is an interactive ops action, not the 5s customer path, so a longer budget is fine.
    public string SummaryModel { get; set; } = "claude-sonnet-5";
    public int SummaryMaxTokens { get; set; } = 512;
    public int SummaryTimeoutSeconds { get; set; } = 15;

    // Guardrail on extraction input size (TDP-AI-01 §2.7) — reject oversized text with 400
    // before any Anthropic call is made.
    public int MaxExtractionInputChars { get; set; } = 4000;
}
