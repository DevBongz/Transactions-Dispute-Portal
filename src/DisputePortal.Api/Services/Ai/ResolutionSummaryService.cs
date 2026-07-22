using System.Globalization;
using System.Text;
using DisputePortal.Api.Contracts.Ai;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Repositories;
using DisputePortal.Api.Services.Ai.Prompts;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Services.Ai;

/// <summary>
/// <see cref="IResolutionSummaryService"/> implementation (TDP-AI-03 §2.4–2.6). Loads the real
/// dispute + transaction to assemble the model's user message (rather than trusting only the
/// request body), calls <c>claude-sonnet-5</c> with the verbatim Feature-3 system prompt, and
/// returns the trimmed plain-text summary. No persistence, no Kafka publish.
/// </summary>
public sealed class ResolutionSummaryService(
    IAnthropicClient client,
    IDisputeRepository repository,
    IOptions<GeminiOptions> options) : IResolutionSummaryService
{
    private readonly GeminiOptions _opts = options.Value;

    public async Task<GenerateSummaryResponse> GenerateAsync(GenerateSummaryRequest request, CancellationToken ct)
    {
        // Validate outcome + notes here too (defence in depth beyond DataAnnotations).
        if (!Enum.TryParse<ResolutionOutcome>(request.Outcome, ignoreCase: false, out var outcome)
            || !Enum.IsDefined(outcome))
            throw new ValidationException($"'{request.Outcome}' is not a valid outcome.");

        if (string.IsNullOrWhiteSpace(request.InternalNotes) || request.InternalNotes.Trim().Length < 20)
            throw new ValidationException("Internal notes must be at least 20 characters.");

        var dispute = await repository.GetForDetailAsync(request.DisputeId, ct)
                      ?? throw new NotFoundException("Dispute not found.");

        var userMessage = BuildUserMessage(dispute, outcome, request.InternalNotes.Trim());

        var summary = await client.CompleteAsync(new AnthropicCompletion(
            _opts.SummaryModel,
            _opts.SummaryMaxTokens,
            SystemPrompts.ResolutionSummary,
            userMessage,
            TimeSpan.FromSeconds(_opts.SummaryTimeoutSeconds)), ct);

        // IAnthropicClient already guarantees a non-empty, trimmed text block (else it throws 502-worthy).
        return new GenerateSummaryResponse(summary);
    }

    // SPEC §3.5 Feature 3 user-message shape.
    private static string BuildUserMessage(Dispute dispute, ResolutionOutcome outcome, string notes)
    {
        var t = dispute.Transaction;
        var amount = t.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var date = t.TransactionDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        return new StringBuilder()
            .Append("Dispute reference: ").AppendLine(dispute.Reference)
            .Append("Transaction: ").Append(t.Currency).Append(' ').Append(amount)
                .Append(" at ").Append(t.MerchantName).Append(" on ").AppendLine(date)
            .Append("Outcome: ").AppendLine(outcome.ToString())
            .Append("Internal notes: \"").Append(notes).Append('"')
            .ToString();
    }
}
