using DisputePortal.Api.Services.Ai;

namespace DisputePortal.IntegrationTests.Fakes;

/// <summary>Deterministic LLM stub for extraction / summary integration tests.</summary>
public sealed class StubAnthropicClient : IAnthropicClient
{
    public string ExtractionJson { get; set; } =
        """{"transactionRef":null,"category":"DUPLICATE_CHARGE","amount":450,"merchantName":"Shoprite","transactionDate":"2026-07-14","confidence":{"transactionRef":0.0,"category":0.9,"amount":0.95,"merchantName":0.95,"transactionDate":0.9}}""";

    public Task<string> CompleteAsync(AnthropicCompletion request, CancellationToken ct) =>
        Task.FromResult(ExtractionJson);
}
