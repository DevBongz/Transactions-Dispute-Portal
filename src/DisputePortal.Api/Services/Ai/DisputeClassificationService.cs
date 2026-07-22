using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Services.Ai.Prompts;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// <see cref="IDisputeClassificationService"/> implementation (TDP-AI-02 §2.4/§2.5). Builds the
/// Feature-2 prompt, calls <c>claude-haiku-4-5-20251001</c> (with a single retry on a transient
/// upstream error), and validates the returned category/priority against the allowed sets.
/// Any failure — transport, timeout, unparseable JSON, or out-of-set value — is captured as a
/// failed result rather than thrown, so the consumer can fall back to <c>CLASSIFICATION_FAILED</c>.
/// </summary>
public sealed class DisputeClassificationService(
    IAnthropicClient client,
    IOptions<GeminiOptions> options,
    ILogger<DisputeClassificationService> logger) : IDisputeClassificationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly GeminiOptions _opts = options.Value;

    public async Task<ClassificationResult> ClassifyAsync(ClassificationContext context, CancellationToken ct)
    {
        var completion = new AnthropicCompletion(
            _opts.ClassificationModel,
            _opts.ClassificationMaxTokens,
            SystemPrompts.Classification,
            BuildUserMessage(context),
            TimeSpan.FromSeconds(_opts.ClassificationTimeoutSeconds));

        string raw;
        try
        {
            raw = await CallWithSingleRetryAsync(completion, ct);
        }
        catch (AnthropicException ex)
        {
            logger.LogWarning("Classification upstream call failed: {Reason}", ex.Message);
            return ClassificationResult.Failed($"llm_error: {ex.Message}");
        }

        var json = ModelJson.ExtractFirstObject(raw);
        if (json is null)
            return ClassificationResult.Failed("no_json_object");

        ClassificationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ClassificationPayload>(json, Json);
        }
        catch (JsonException)
        {
            return ClassificationResult.Failed("unparseable_json");
        }

        if (payload is null)
            return ClassificationResult.Failed("null_payload");

        if (!TryParse<DisputeCategory>(payload.Category, out var category))
            return ClassificationResult.Failed($"invalid_category: {payload.Category}");

        if (!TryParse<DisputePriority>(payload.Priority, out var priority))
            return ClassificationResult.Failed($"invalid_priority: {payload.Priority}");

        return ClassificationResult.Ok(category, priority, payload.Rationale?.Trim());
    }

    // One immediate retry only for transient upstream conditions (429/5xx/timeout); deterministic
    // errors are not retried (they are handled by the caller as parse failures). Keeps within the
    // ~5s budget (TDP-AI-02 §2.7).
    private async Task<string> CallWithSingleRetryAsync(AnthropicCompletion completion, CancellationToken ct)
    {
        try
        {
            return await client.CompleteAsync(completion, ct);
        }
        catch (AnthropicException ex) when (ex.Transient && !ct.IsCancellationRequested)
        {
            logger.LogInformation("Transient Gemini error on classification; retrying once.");
            return await client.CompleteAsync(completion, ct);
        }
    }

    private static bool TryParse<TEnum>(string? raw, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return Enum.TryParse(raw, ignoreCase: false, out value) && Enum.IsDefined(value);
    }

    // SPEC §3.5 Feature 2 user-message context.
    private static string BuildUserMessage(ClassificationContext c)
    {
        var amount = c.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var date = c.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var mcc = string.IsNullOrWhiteSpace(c.MerchantCategory) ? "unknown" : c.MerchantCategory;

        return new StringBuilder()
            .Append("Transaction: { merchant: \"").Append(c.MerchantName)
                .Append("\", amount: ").Append(amount)
                .Append(", date: \"").Append(date)
                .Append("\", merchantCategory: \"").Append(mcc).AppendLine("\" }")
            .Append("Customer description: \"").Append(c.CustomerDescription).AppendLine("\"")
            .Append("Customer open dispute count: ").Append(c.CustomerOpenDisputeCount)
            .ToString();
    }

    private sealed record ClassificationPayload(
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("priority")] string? Priority,
        [property: JsonPropertyName("rationale")] string? Rationale);
}
