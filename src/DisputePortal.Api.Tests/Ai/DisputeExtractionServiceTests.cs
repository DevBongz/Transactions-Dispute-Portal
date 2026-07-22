using DisputePortal.Api.Services.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace DisputePortal.Api.Tests.Ai;

/// <summary>
/// Unit tests for <see cref="DisputeExtractionService"/> (TDP-AI-01 §5 DoD): JSON parsing,
/// category allow-list enforcement, confidence-map normalization, and 502 mapping on
/// unparseable output. The Anthropic client is stubbed — no live calls (SPEC §4.4).
/// </summary>
public sealed class DisputeExtractionServiceTests
{
    private static DisputeExtractionService ServiceWith(FakeAnthropicClient client) =>
        new(client, Options.Create(new AnthropicOptions()));

    [Fact]
    public async Task Parses_fields_and_populates_confidence_for_every_field()
    {
        const string json = """
        { "category": "DUPLICATE_CHARGE", "amount": 450.00, "merchantName": "Shoprite",
          "transactionDate": "2026-07-14",
          "confidence": { "category": 0.94, "amount": 0.88, "merchantName": 0.91, "transactionDate": 0.72 } }
        """;
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text(json)));

        var result = await service.ExtractAsync("charged twice at Shoprite", CancellationToken.None);

        Assert.Equal("DUPLICATE_CHARGE", result.Category);
        Assert.Equal(450.00m, result.Amount);
        Assert.Equal("Shoprite", result.MerchantName);
        Assert.Equal("2026-07-14", result.TransactionDate);

        // Every field name carries a confidence entry (AC-DISP-02 UI rule).
        Assert.Equal(
            new[] { "transactionRef", "category", "amount", "merchantName", "transactionDate" }.OrderBy(x => x),
            result.Confidence.Keys.OrderBy(x => x));
        Assert.Equal(0.94, result.Confidence["category"], precision: 3);
        Assert.Equal(0.0, result.Confidence["transactionRef"]); // absent → 0.0
    }

    [Fact]
    public async Task Drops_out_of_set_category_with_zero_confidence()
    {
        const string json = """{ "category": "NONSENSE", "confidence": { "category": 0.99 } }""";
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text(json)));

        var result = await service.ExtractAsync("something", CancellationToken.None);

        Assert.Null(result.Category);
        Assert.Equal(0.0, result.Confidence["category"]);
    }

    [Fact]
    public async Task Extracts_json_even_when_wrapped_in_prose()
    {
        const string body = "Sure! Here is the JSON:\n{ \"merchantName\": \"Checkers\" }\nHope that helps.";
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text(body)));

        var result = await service.ExtractAsync("bought at Checkers", CancellationToken.None);

        Assert.Equal("Checkers", result.MerchantName);
    }

    [Fact]
    public async Task Unparseable_output_throws_AnthropicException()
    {
        var service = ServiceWith(new FakeAnthropicClient(FakeAnthropicClient.Text("no json here at all")));

        await Assert.ThrowsAsync<AnthropicException>(
            () => service.ExtractAsync("whatever", CancellationToken.None));
    }
}
