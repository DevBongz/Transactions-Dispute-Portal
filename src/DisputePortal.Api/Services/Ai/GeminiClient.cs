using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Google Gemini <c>generateContent</c> implementation of <see cref="IAnthropicClient"/>.
/// The interface name is historical (Batch 5 used Anthropic); the contract is a single
/// non-streaming text completion. The API key is sent as a query parameter (never logged).
/// All failures are wrapped in <see cref="AnthropicException"/> so callers map to HTTP 502.
/// </summary>
public sealed class GeminiClient(
    HttpClient http,
    IOptions<GeminiOptions> options,
    ILogger<GeminiClient> logger) : IAnthropicClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _apiKey = (options.Value.ApiKey ?? string.Empty).Trim();

    public async Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new AnthropicException("Gemini API key is not configured.", transient: false);

        // Include the system prompt in the user turn as well — more reliable across model revisions.
        var userText = string.IsNullOrWhiteSpace(request.System)
            ? request.UserMessage
            : $"{request.System}\n\n---\n\n{request.UserMessage}";

        var body = new GenerateContentRequest(
            SystemInstruction: string.IsNullOrWhiteSpace(request.System)
                ? null
                : new ContentDto([new PartDto(request.System)]),
            Contents: [new ContentDto([new PartDto(userText)], Role: "user")],
            GenerationConfig: new GenerationConfigDto(request.MaxTokens));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(request.Timeout);

        // Key as query param avoids header-validation failures on keys with whitespace/newlines.
        var path =
            $"/v1beta/models/{Uri.EscapeDataString(request.Model)}:generateContent" +
            $"?key={Uri.EscapeDataString(_apiKey)}";

        var started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(path, body, Json, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini request timed out after {TimeoutMs}ms (model {Model})",
                request.Timeout.TotalMilliseconds, request.Model);
            throw new AnthropicException("Gemini request timed out.", transient: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Gemini request failed at the transport layer (model {Model})", request.Model);
            throw new AnthropicException("Gemini request failed.", transient: true, inner: ex);
        }

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests
                                || (int)response.StatusCode >= 500;
                logger.LogWarning(
                    "Gemini returned {StatusCode} for model {Model} in {ElapsedMs:0}ms (transient={Transient})",
                    (int)response.StatusCode, request.Model, elapsedMs, transient);
                throw new AnthropicException(
                    $"Gemini responded with status {(int)response.StatusCode}.", transient);
            }

            GenerateContentResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Gemini response envelope was not valid JSON (model {Model})", request.Model);
                throw new AnthropicException("Gemini response could not be parsed.", inner: ex);
            }

            var text = parsed?.Candidates?
                .SelectMany(c => c.Content?.Parts ?? [])
                .Select(p => p.Text)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

            if (string.IsNullOrWhiteSpace(text))
            {
                var block = parsed?.PromptFeedback?.BlockReason;
                logger.LogWarning(
                    "Gemini response contained no text (model {Model}, blockReason {BlockReason})",
                    request.Model, block ?? "-");
                throw new AnthropicException(
                    string.IsNullOrEmpty(block)
                        ? "Gemini response contained no text."
                        : $"Gemini blocked the prompt ({block}).");
            }

            logger.LogInformation(
                "Gemini completion succeeded (ai.model {Model}, ai.durationMs {ElapsedMs:0})",
                request.Model, elapsedMs);

            return text.Trim();
        }
    }

    private sealed record GenerateContentRequest(
        [property: JsonPropertyName("systemInstruction")] ContentDto? SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<ContentDto> Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfigDto GenerationConfig);

    private sealed record GenerationConfigDto(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);

    private sealed record ContentDto(
        [property: JsonPropertyName("parts")] IReadOnlyList<PartDto> Parts,
        [property: JsonPropertyName("role")] string? Role = null);

    private sealed record PartDto(
        [property: JsonPropertyName("text")] string Text);

    private sealed record GenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<CandidateDto>? Candidates,
        [property: JsonPropertyName("promptFeedback")] PromptFeedbackDto? PromptFeedback);

    private sealed record CandidateDto(
        [property: JsonPropertyName("content")] ContentDto? Content);

    private sealed record PromptFeedbackDto(
        [property: JsonPropertyName("blockReason")] string? BlockReason);
}
