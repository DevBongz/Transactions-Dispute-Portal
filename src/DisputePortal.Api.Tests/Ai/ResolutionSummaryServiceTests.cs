using DisputePortal.Api.Contracts.Ai;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Services.Ai;
using DisputePortal.Api.Tests.Disputes;
using Microsoft.Extensions.Options;
using Xunit;

namespace DisputePortal.Api.Tests.Ai;

/// <summary>
/// Unit tests for <see cref="ResolutionSummaryService"/> (TDP-AI-03 §5 DoD): outcome/notes
/// validation (400), unknown dispute (404), and the plain-text happy path. Anthropic and the
/// repository are stubbed (SPEC §4.4).
/// </summary>
public sealed class ResolutionSummaryServiceTests
{
    private const string ValidNotes = "Transaction confirmed as duplicate — refund initiated.";

    private static ResolutionSummaryService ServiceWith(FakeAnthropicClient client, FakeDisputeRepository repo) =>
        new(client, repo, Options.Create(new AnthropicOptions()));

    private static Dispute DisputeWithTransaction() => new()
    {
        Id = Guid.NewGuid(),
        Reference = "DSP-20260714-00042",
        Transaction = new Transaction
        {
            MerchantName = "Shoprite",
            Amount = 450.00m,
            Currency = "ZAR",
            TransactionDate = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)
        }
    };

    [Fact]
    public async Task Invalid_outcome_throws_Validation()
    {
        var service = ServiceWith(new FakeAnthropicClient(), new FakeDisputeRepository());
        var req = new GenerateSummaryRequest(Guid.NewGuid(), "NOT_AN_OUTCOME", ValidNotes);

        await Assert.ThrowsAsync<ValidationException>(() => service.GenerateAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task Too_short_notes_throws_Validation()
    {
        var service = ServiceWith(new FakeAnthropicClient(), new FakeDisputeRepository());
        var req = new GenerateSummaryRequest(Guid.NewGuid(), "UPHELD", "too short");

        await Assert.ThrowsAsync<ValidationException>(() => service.GenerateAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_dispute_throws_NotFound()
    {
        var service = ServiceWith(new FakeAnthropicClient(), new FakeDisputeRepository { Detail = null });
        var req = new GenerateSummaryRequest(Guid.NewGuid(), "UPHELD", ValidNotes);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_trimmed_summary_on_happy_path()
    {
        // IAnthropicClient's contract is to return already-trimmed text (the real client trims).
        const string summary = "We've refunded your duplicate R450 charge at Shoprite.";
        var client = new FakeAnthropicClient(FakeAnthropicClient.Text(summary));
        var repo = new FakeDisputeRepository { Detail = DisputeWithTransaction() };
        var service = ServiceWith(client, repo);
        var req = new GenerateSummaryRequest(Guid.NewGuid(), "UPHELD", ValidNotes);

        var result = await service.GenerateAsync(req, CancellationToken.None);

        Assert.Equal(summary, result.Summary);
        Assert.Equal(1, client.Calls);
    }
}
