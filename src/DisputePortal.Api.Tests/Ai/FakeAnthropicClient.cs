using DisputePortal.Api.Services.Ai;

namespace DisputePortal.Api.Tests.Ai;

/// <summary>
/// Deterministic <see cref="IAnthropicClient"/> stub for AI service unit tests — no live API
/// calls (SPEC §4.4). Configured with an ordered set of responses; each response is either a
/// text body or a thrown exception, letting a test model the happy path, a parse edge case,
/// or a transient-then-success retry sequence.
/// </summary>
internal sealed class FakeAnthropicClient : IAnthropicClient
{
    private readonly Queue<Func<string>> _responses;

    public int Calls { get; private set; }

    public FakeAnthropicClient(params Func<string>[] responses) => _responses = new Queue<Func<string>>(responses);

    /// <summary>A response that returns the given assistant text.</summary>
    public static Func<string> Text(string body) => () => body;

    /// <summary>A response that throws — models an upstream failure/timeout.</summary>
    public static Func<string> Fail(Exception ex) => () => throw ex;

    public Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct)
    {
        Calls++;
        var next = _responses.Count > 0
            ? _responses.Dequeue()
            : throw new InvalidOperationException("FakeAnthropicClient has no more configured responses.");

        try { return Task.FromResult(next()); }
        catch (Exception ex) { return Task.FromException<string>(ex); }
    }
}
