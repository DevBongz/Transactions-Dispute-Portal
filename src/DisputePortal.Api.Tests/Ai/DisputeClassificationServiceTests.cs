using DisputePortal.Api.Domain;
using DisputePortal.Api.Services.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DisputePortal.Api.Tests.Ai;

/// <summary>
/// Unit tests for <see cref="DisputeClassificationService"/> (TDP-AI-02 §5 DoD): category/priority
/// mapping, the non-blocking failure fallback on AI error / out-of-set output, and the single
/// transient retry. The service never throws — it returns a failed <see cref="ClassificationResult"/>
/// so the consumer can flag <c>CLASSIFICATION_FAILED</c> and still commit the offset.
/// </summary>
public sealed class DisputeClassificationServiceTests
{
    private static DisputeClassificationService ServiceWith(FakeAnthropicClient client) =>
        new(client, Options.Create(new AnthropicOptions()),
            NullLogger<DisputeClassificationService>.Instance);

    private static ClassificationContext Context() =>
        new("Shoprite", "Groceries", 450.00m, DateTimeOffset.UtcNow, "charged twice", CustomerOpenDisputeCount: 0);

    [Fact]
    public async Task Maps_valid_category_and_priority()
    {
        const string json = """{ "category": "DUPLICATE_CHARGE", "priority": "HIGH", "rationale": "Two identical charges." }""";
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text(json)));

        var result = await service.ClassifyAsync(Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DisputeCategory.DUPLICATE_CHARGE, result.Category);
        Assert.Equal(DisputePriority.HIGH, result.Priority);
        Assert.Equal("Two identical charges.", result.Rationale);
    }

    [Fact]
    public async Task Out_of_set_category_is_a_failure()
    {
        const string json = """{ "category": "WHATEVER", "priority": "HIGH" }""";
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text(json)));

        var result = await service.ClassifyAsync(Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Category);
        Assert.Contains("invalid_category", result.FailureReason);
    }

    [Fact]
    public async Task Anthropic_failure_falls_back_without_throwing()
    {
        var client = new FakeAnthropicClient(
            FakeAnthropicClient.Fail(new AnthropicException("boom", transient: false)));
        var service = ServiceWith(client);

        var result = await service.ClassifyAsync(Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("anthropic_error", result.FailureReason);
        Assert.Equal(1, client.Calls); // non-transient → no retry
    }

    [Fact]
    public async Task Transient_error_is_retried_once_then_succeeds()
    {
        const string json = """{ "category": "UNAUTHORISED", "priority": "CRITICAL" }""";
        var client = new FakeAnthropicClient(
            FakeAnthropicClient.Fail(new AnthropicException("429", transient: true)),
            FakeAnthropicClient.Text(json));
        var service = ServiceWith(client);

        var result = await service.ClassifyAsync(Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DisputeCategory.UNAUTHORISED, result.Category);
        Assert.Equal(DisputePriority.CRITICAL, result.Priority);
        Assert.Equal(2, client.Calls); // one retry
    }
}
