using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Typed <see cref="HttpClient"/> implementation of <see cref="IAnthropicClient"/>
/// (TDP-AI-01 §2.3). The base address, <c>x-api-key</c> and <c>anthropic-version</c> headers
/// are set once on the registered client (in <c>Program.cs</c>) so the API key is never
/// serialized per-request or logged. Per-call timeouts are enforced with a linked
/// <see cref="CancellationTokenSource"/> rather than <see cref="HttpClient.Timeout"/>, letting
/// each feature carry its own budget through the one shared client.
/// </summary>
public sealed class AnthropicClient(HttpClient http, ILogger<AnthropicClient> logger) : IAnthropicClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct)
    {
        var body = new MessagesRequest(
            request.Model,
            request.MaxTokens,
            request.System,
            [new MessageDto("user", request.UserMessage)]);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(request.Timeout);

        var started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/v1/messages", body, Json, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our timeout fired, not the caller's cancellation.
            logger.LogWarning("Anthropic request timed out after {TimeoutMs}ms (model {Model})",
                request.Timeout.TotalMilliseconds, request.Model);
            throw new AnthropicException("Anthropic request timed out.", transient: true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Anthropic request failed at the transport layer (model {Model})", request.Model);
            throw new AnthropicException("Anthropic request failed.", transient: true, inner: ex);
        }

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Do NOT log the response body — it may echo request context. Never log the API key.
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests
                                || (int)response.StatusCode >= 500;
                logger.LogWarning(
                    "Anthropic returned {StatusCode} for model {Model} in {ElapsedMs:0}ms (transient={Transient})",
                    (int)response.StatusCode, request.Model, elapsedMs, transient);
                throw new AnthropicException($"Anthropic responded with status {(int)response.StatusCode}.", transient);
            }

            MessagesResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<MessagesResponse>(Json, ct);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Anthropic response envelope was not valid JSON (model {Model})", request.Model);
                throw new AnthropicException("Anthropic response could not be parsed.", inner: ex);
            }

            var text = parsed?.Content?
                .FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.Ordinal))?
                .Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                logger.LogWarning("Anthropic response contained no text block (model {Model})", request.Model);
                throw new AnthropicException("Anthropic response contained no text.");
            }

            logger.LogInformation(
                "Anthropic completion succeeded (ai.model {Model}, ai.durationMs {ElapsedMs:0}, ai.stopReason {StopReason})",
                request.Model, elapsedMs, parsed!.StopReason ?? "-");

            return text.Trim();
        }
    }

    // ---- wire DTOs (Anthropic Messages API) ----

    private sealed record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<MessageDto> Messages);

    private sealed record MessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record MessagesResponse(
        [property: JsonPropertyName("stop_reason")] string? StopReason,
        [property: JsonPropertyName("content")] IReadOnlyList<ContentBlock>? Content);

    private sealed record ContentBlock(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}
