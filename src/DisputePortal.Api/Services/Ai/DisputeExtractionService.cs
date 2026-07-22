using System.Text.Json;
using System.Text.Json.Serialization;
using DisputePortal.Api.Contracts.Ai;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Services.Ai.Prompts;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// <see cref="IDisputeExtractionService"/> implementation (TDP-AI-01 §2.5). Builds the
/// Feature-1 prompt, calls <see cref="IAnthropicClient"/> with the extraction model, then
/// deserializes the model's JSON defensively: unknown categories are dropped, and the
/// confidence map is normalized so every field name carries a numeric entry for the UI.
/// </summary>
public sealed class DisputeExtractionService(
    IAnthropicClient client, IOptions<GeminiOptions> options) : IDisputeExtractionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // The five field names the response contract always reports a confidence for.
    private static readonly string[] FieldNames =
        ["transactionRef", "category", "amount", "merchantName", "transactionDate"];

    private readonly GeminiOptions _opts = options.Value;

    public async Task<ExtractDisputeResponse> ExtractAsync(string text, CancellationToken ct)
    {
        var raw = await client.CompleteAsync(new AnthropicCompletion(
            _opts.ExtractionModel,
            _opts.ExtractionMaxTokens,
            SystemPrompts.Extraction,
            text,
            TimeSpan.FromSeconds(_opts.ExtractionTimeoutSeconds)), ct);

        var json = ModelJson.ExtractFirstObject(raw)
                   ?? throw new AnthropicException("Extraction response did not contain a JSON object.");

        ExtractionPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<ExtractionPayload>(json, Json)
                      ?? throw new AnthropicException("Extraction response deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new AnthropicException("Extraction response was not valid JSON.", inner: ex);
        }

        // Validate category against the allowed set; drop (null + confidence 0) if out of set.
        var category = ParseCategory(payload.Category);

        var confidence = FieldNames.ToDictionary(
            f => f,
            f =>
            {
                var value = payload.Confidence is not null && payload.Confidence.TryGetValue(f, out var c) ? c : 0.0;
                // A dropped category cannot claim confidence.
                if (f == "category" && category is null) return 0.0;
                return Math.Clamp(value, 0.0, 1.0);
            });

        return new ExtractDisputeResponse(
            TransactionRef: NullIfBlank(payload.TransactionRef),
            Category: category?.ToString(),
            Amount: payload.Amount,
            MerchantName: NullIfBlank(payload.MerchantName),
            TransactionDate: NullIfBlank(payload.TransactionDate),
            Confidence: confidence);
    }

    private static DisputeCategory? ParseCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Enum.TryParse<DisputeCategory>(raw, ignoreCase: false, out var c) && Enum.IsDefined(c)
            ? c
            : null;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Intermediate shape of the model's JSON (fields optional; confidence is a loose map).
    private sealed record ExtractionPayload(
        [property: JsonPropertyName("transactionRef")] string? TransactionRef,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("amount")] decimal? Amount,
        [property: JsonPropertyName("merchantName")] string? MerchantName,
        [property: JsonPropertyName("transactionDate")] string? TransactionDate,
        [property: JsonPropertyName("confidence")] Dictionary<string, double>? Confidence);
}
