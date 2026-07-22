using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Body for <c>POST /disputes</c> (TDP-DISP-01 §2.3, SPEC §3.3). <paramref name="Category"/>
/// is usually null at submit (set later by classification, TDP-AI-02); if supplied it must
/// be a valid <see cref="Domain.DisputeCategory"/> or the request is rejected 400.
/// <paramref name="ExtractedFields"/> is stored verbatim into the <c>extracted_fields_json</c>
/// JSONB column.
/// </summary>
public sealed record SubmitDisputeRequest(
    Guid TransactionId,
    string? Category,
    [Required, MinLength(10)] string Description,
    JsonElement? ExtractedFields);
