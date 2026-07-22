namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Raised when an LLM completion call cannot produce a usable result — non-2xx,
/// timeout, empty text, or unparseable envelope. <see cref="Transient"/> is true for
/// rate-limits / 5xx / timeouts so callers (e.g. classification) can retry once.
/// Named historically for Anthropic; used for Gemini as well.
/// </summary>
public sealed class AnthropicException : Exception
{
    public bool Transient { get; }

    public AnthropicException(string message, bool transient = false, Exception? inner = null)
        : base(message, inner) => Transient = transient;
}
