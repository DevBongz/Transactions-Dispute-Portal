using System.Text.Json;
using DisputePortal.Api.Contracts.Transactions;

namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Full dispute detail (TDP-DISP-02 §2.2): the dispute, its originating transaction,
/// the resolution (null until resolved), and the chronological event timeline.
/// </summary>
public sealed record DisputeDetailDto(
    Guid Id,
    string Reference,
    string Status,
    string? Category,
    string? Priority,
    string CustomerDescription,
    JsonElement? ExtractedFields,
    Guid? AssignedToId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    TransactionDto Transaction,
    ResolutionDto? Resolution,
    IReadOnlyList<DisputeEventDto> Timeline);
