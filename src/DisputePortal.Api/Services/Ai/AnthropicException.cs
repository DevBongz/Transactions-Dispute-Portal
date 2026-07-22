namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// Raised when a call to the Anthropic Messages API cannot produce a usable result —
/// a non-2xx status, a timeout, an empty/mangled body, or a missing text block
/// (TDP-AI-01 §2.7, TDP-AI-03 §2.8). Callers translate this into an HTTP <c>502</c> so the
/// customer/analyst can still fall back to manual entry. The message never contains the
/// API key or raw upstream credentials (SPEC §3.6 Security).
/// </summary>
public sealed class AnthropicException : Exception
{
    /// <summary>True for transient conditions (429/5xx/timeout) eligible for a single retry.</summary>
    public bool Transient { get; }

    public AnthropicException(string message, bool transient = false, Exception? inner = null)
        : base(message, inner) => Transient = transient;
}
