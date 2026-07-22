using System.Security.Claims;
using System.Text.Json;
using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Contracts.Transactions;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Auth;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Messaging;
using DisputePortal.Api.Messaging.Events;
using DisputePortal.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DisputePortal.Api.Services;

/// <summary>
/// <see cref="IDisputeService"/> implementation (TDP-DISP-01/02/03). Uses the DbContext as the
/// unit-of-work for the transactional write flows (submit / status / resolve) — mirroring the
/// ticket's service design and the existing direct-DbContext usage in AuthController — while
/// delegating all read projections and query helpers to <see cref="IDisputeRepository"/>.
/// Domain events are always published <em>after</em> the DB transaction commits so a rollback
/// can never leave a phantom event (TDP-DISP-01 §2.5/§2.7).
/// </summary>
public sealed class DisputeService(
    DisputePortalDbContext db,
    IDisputeRepository repository,
    IDisputeReferenceGenerator referenceGenerator,
    IEventPublisher publisher) : IDisputeService
{
    private const int MaxPageSize = 100;
    private const int MaxReferenceAttempts = 3;

    // Constraint names from the InitialCreate migration — used to distinguish which unique
    // index a Postgres 23505 violated (reference sequence race vs. duplicate-dispute race).
    private const string ReferenceIndex = "IX_disputes_reference";
    private const string TransactionIndex = "IX_disputes_transaction_id";

    // Allowed manual status transitions (TDP-DISP-02 §2.5). RESOLVED is reachable only via
    // the resolve endpoint, never this map.
    private static readonly Dictionary<DisputeStatus, DisputeStatus[]> AllowedTransitions = new()
    {
        [DisputeStatus.OPEN] = [DisputeStatus.UNDER_REVIEW],
        [DisputeStatus.UNDER_REVIEW] = [DisputeStatus.OPEN],
        [DisputeStatus.CLASSIFICATION_FAILED] = [DisputeStatus.OPEN, DisputeStatus.UNDER_REVIEW],
    };

    public async Task<SubmitDisputeResponse> SubmitDisputeAsync(
        Guid customerId, SubmitDisputeRequest req, CancellationToken ct)
    {
        // Category is normally null at submit; if supplied it must be a valid enum (else 400).
        var category = ParseNullable<DisputeCategory>(req.Category, "category");

        // Ownership + existence: transaction must exist AND belong to the caller (else 404).
        _ = await repository.GetOwnedTransactionAsync(customerId, req.TransactionId, ct)
            ?? throw new NotFoundException("Transaction not found.");

        // Duplicate-dispute guard: a transaction has zero-or-one dispute (SPEC §3.2).
        if (await repository.ExistsForTransactionAsync(req.TransactionId, ct))
            throw new ConflictException("A dispute already exists for this transaction.");

        var extractedFields = req.ExtractedFields?.GetRawText();

        for (var attempt = 1; ; attempt++)
        {
            var now = DateTimeOffset.UtcNow;
            var reference = await referenceGenerator.GenerateAsync(DateOnly.FromDateTime(now.UtcDateTime), ct);

            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                TransactionId = req.TransactionId,
                CustomerId = customerId,
                Status = DisputeStatus.OPEN,
                Category = category,
                Priority = null,
                CustomerDescription = req.Description,
                ExtractedFieldsJson = extractedFields,
                CreatedAt = now,
                UpdatedAt = now
            };
            var submitted = new DisputeEvent
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                EventType = DisputeEventType.SUBMITTED,
                ActorId = customerId,
                Description = $"Dispute {reference} submitted by customer.",
                CreatedAt = now
            };

            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                db.Disputes.Add(dispute);
                db.DisputeEvents.Add(submitted);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex, ReferenceIndex) && attempt < MaxReferenceAttempts)
            {
                // Concurrent same-day submission grabbed this sequence — detach and retry.
                Detach(dispute, submitted);
                continue;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex, TransactionIndex))
            {
                // Lost a race on the duplicate-dispute guard.
                throw new ConflictException("A dispute already exists for this transaction.");
            }

            // Publish AFTER commit so the event never references an uncommitted row (§2.7).
            await publisher.PublishAsync(new DisputeSubmittedEvent(
                dispute.Id, dispute.Reference, dispute.TransactionId,
                dispute.CustomerId, Category: null, dispute.CustomerDescription), ct);

            return new SubmitDisputeResponse(dispute.Id, dispute.Reference, dispute.Status.ToString());
        }
    }

    public Task<PagedResult<DisputeSummaryDto>> ListAsync(
        ClaimsPrincipal caller, DisputeQuery query, CancellationToken ct)
    {
        var status = ParseNullable<DisputeStatus>(query.Status, "status");
        var priority = ParseNullable<DisputePriority>(query.Priority, "priority");
        var category = ParseNullable<DisputeCategory>(query.Category, "category");

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize < 1 ? 20 : query.PageSize, 1, MaxPageSize);

        var isCustomer = caller.IsInRole(nameof(UserRole.CUSTOMER));
        return repository.ListAsync(isCustomer, caller.GetUserId(), page, pageSize, status, priority, category, ct);
    }

    public async Task<DisputeDetailDto?> GetDetailAsync(ClaimsPrincipal caller, Guid id, CancellationToken ct)
    {
        var dispute = await repository.GetForDetailAsync(id, ct);
        if (dispute is null) return null;

        // Row-level security: a customer may only view their own dispute (404, no leak).
        if (caller.IsInRole(nameof(UserRole.CUSTOMER)) && dispute.CustomerId != caller.GetUserId())
            return null;

        var transaction = new TransactionDto(
            dispute.Transaction.Id,
            dispute.Transaction.Reference,
            dispute.Transaction.MerchantName,
            dispute.Transaction.MerchantCategory,
            dispute.Transaction.Amount,
            dispute.Transaction.Currency,
            dispute.Transaction.TransactionDate,
            dispute.Transaction.Status.ToString(),
            HasDispute: true);

        var resolution = dispute.Resolution is null
            ? null
            : new ResolutionDto(
                dispute.Resolution.Outcome.ToString(),
                dispute.Resolution.CustomerSummary,
                dispute.Resolution.ResolvedById,
                dispute.Resolution.ResolvedAt);

        var timeline = dispute.Events
            .OrderBy(e => e.CreatedAt)
            .Select(e => new DisputeEventDto(
                e.EventType.ToString(), e.Description, e.ActorId, e.Actor?.FullName, e.CreatedAt))
            .ToList();

        return new DisputeDetailDto(
            dispute.Id,
            dispute.Reference,
            dispute.Status.ToString(),
            dispute.Category?.ToString(),
            dispute.Priority?.ToString(),
            dispute.CustomerDescription,
            ParseJson(dispute.ExtractedFieldsJson),
            dispute.AssignedToId,
            dispute.CustomerId,
            dispute.Customer.FullName,
            dispute.Customer.Email,
            transaction,
            resolution,
            timeline);
    }

    public async Task<DisputeSummaryDto> UpdateStatusAsync(
        Guid opsUserId, Guid id, UpdateStatusRequest req, CancellationToken ct)
    {
        var target = ParseRequired<DisputeStatus>(req.Status, "status");

        var dispute = await repository.GetTrackedAsync(id, ct)
            ?? throw new NotFoundException("Dispute not found.");

        // Idempotent no-op: setting the current status again does not append an event (§2.5 notes).
        if (dispute.Status != target)
        {
            if (!AllowedTransitions.TryGetValue(dispute.Status, out var allowed) || !allowed.Contains(target))
                throw new ConflictException($"Cannot transition dispute from {dispute.Status} to {target}.");

            var now = DateTimeOffset.UtcNow;
            dispute.Status = target;
            dispute.UpdatedAt = now;

            // Moving to UNDER_REVIEW assigns the acting analyst if unassigned (OPS-03).
            if (target == DisputeStatus.UNDER_REVIEW && dispute.AssignedToId is null)
                dispute.AssignedToId = opsUserId;

            db.DisputeEvents.Add(new DisputeEvent
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                EventType = target == DisputeStatus.UNDER_REVIEW
                    ? DisputeEventType.UNDER_REVIEW
                    : DisputeEventType.REOPENED,   // the only other allowed target is OPEN
                ActorId = opsUserId,
                Description = $"Status changed to {target} by ops.",
                CreatedAt = now
            });

            await db.SaveChangesAsync(ct);
        }

        return new DisputeSummaryDto(
            dispute.Id, dispute.Reference, dispute.TransactionId, dispute.CustomerId,
            dispute.Customer.FullName, dispute.Status.ToString(),
            dispute.Category?.ToString(), dispute.Priority?.ToString(),
            dispute.CreatedAt, dispute.UpdatedAt);
    }

    public async Task<ResolutionResponse> ResolveAsync(
        Guid opsUserId, Guid id, ResolveDisputeRequest req, CancellationToken ct)
    {
        var outcome = ParseRequired<ResolutionOutcome>(req.Outcome, "outcome");

        var dispute = await repository.GetTrackedForResolveAsync(id, ct)
            ?? throw new NotFoundException("Dispute not found.");

        // One-to-one guard (SPEC §3.2, resolution.dispute_id UNIQUE).
        if (dispute.Status == DisputeStatus.RESOLVED || dispute.Resolution is not null)
            throw new ConflictException("Dispute is already resolved.");

        var now = DateTimeOffset.UtcNow;
        var resolution = new Resolution
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            Outcome = outcome,
            InternalNotes = req.InternalNotes,
            CustomerSummary = req.CustomerSummary,
            ResolvedById = opsUserId,
            ResolvedAt = now
        };

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Resolutions.Add(resolution);
            dispute.Status = DisputeStatus.RESOLVED;
            dispute.UpdatedAt = now;
            db.DisputeEvents.Add(new DisputeEvent
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                EventType = DisputeEventType.RESOLVED,
                ActorId = opsUserId,
                Description = $"Dispute resolved as {outcome}.",
                CreatedAt = now
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, constraint: null))
        {
            // Concurrent double-resolve tripped the resolution.dispute_id unique index.
            throw new ConflictException("Dispute is already resolved.");
        }

        var summaryProvided = !string.IsNullOrWhiteSpace(req.CustomerSummary);
        await publisher.PublishAsync(new DisputeResolvedEvent(
            dispute.Id, dispute.Reference, outcome.ToString(), opsUserId, summaryProvided), ct);

        return new ResolutionResponse(
            resolution.Id, dispute.Id, outcome.ToString(),
            resolution.CustomerSummary, resolution.ResolvedById, resolution.ResolvedAt);
    }

    // ---- helpers ----

    private void Detach(params object[] entities)
    {
        foreach (var e in entities)
            db.Entry(e).State = EntityState.Detached;
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string? constraint) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
        (constraint is null || pg.ConstraintName == constraint);

    private static JsonElement? ParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    // Strict enum parse: rejects unknown names AND numeric/underlying-value inputs by requiring
    // the input to round-trip to the canonical enum name (SPEC §3.2 uses the names verbatim).
    private static TEnum? ParseNullable<TEnum>(string? raw, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (Enum.TryParse<TEnum>(raw, ignoreCase: false, out var value) &&
            Enum.IsDefined(value) && string.Equals(value.ToString(), raw, StringComparison.Ordinal))
            return value;
        throw new ValidationException($"'{raw}' is not a valid {field}.");
    }

    private static TEnum ParseRequired<TEnum>(string? raw, string field) where TEnum : struct, Enum =>
        ParseNullable<TEnum>(raw, field) ?? throw new ValidationException($"{field} is required.");
}
