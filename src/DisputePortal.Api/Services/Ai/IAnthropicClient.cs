namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Thin wrapper over the Anthropic Messages API (<c>POST /v1/messages</c>) shared by all
/// three AI features (TDP-AI-01 §2.3). Kept deliberately small so it is trivial to mock with
/// a stub <see cref="HttpMessageHandler"/> in tests (SPEC §4.4 — no live API calls in CI).
/// Returns the assistant's text (<c>content[0].text</c>); throws <see cref="AnthropicException"/>
/// on any failure so callers can uniformly map to a 502.
/// </summary>
public interface IAnthropicClient
{
    /// <summary>
    /// Send a single, non-streaming message request and return the assistant text block.
    /// </summary>
    /// <exception cref="AnthropicException">Non-2xx, timeout, or no usable text in the response.</exception>
    Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct);
}

/// <summary>
/// A single Messages API call: which <paramref name="Model"/> to use, the token cap, the
/// system prompt, the user message, and the per-call <paramref name="Timeout"/> budget
/// (enforced independently of the shared <see cref="HttpClient"/> so different features can
/// have different ceilings — 5s extraction/classification vs. a longer summary budget).
/// </summary>
public sealed record AnthropicCompletion(
    string Model,
    int MaxTokens,
    string System,
    string UserMessage,
    TimeSpan Timeout);
