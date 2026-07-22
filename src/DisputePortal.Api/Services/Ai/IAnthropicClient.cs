namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Thin wrapper over a chat/completion LLM API, shared by all three AI features.
/// Historically named for Anthropic; the live implementation is Google Gemini.
/// Returns the assistant text; throws <see cref="AnthropicException"/> on any failure
/// so callers can uniformly map to a 502.
/// </summary>
public interface IAnthropicClient
{
    /// <summary>
    /// Send a single, non-streaming completion request and return the assistant text.
    /// </summary>
    /// <exception cref="AnthropicException">Non-2xx, timeout, or no usable text in the response.</exception>
    Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct);
}

/// <summary>
/// A single LLM call: which <paramref name="Model"/> to use, the token cap, the system
/// prompt, the user message, and the per-call <paramref name="Timeout"/> budget.
/// </summary>
public sealed record AnthropicCompletion(
    string Model,
    int MaxTokens,
    string System,
    string UserMessage,
    TimeSpan Timeout);
