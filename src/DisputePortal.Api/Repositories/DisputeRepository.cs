using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDisputeRepository"/> (TDP-DISP-01/02/03). Ops list
/// results are ordered by a priority CASE ordinal (CRITICAL → HIGH → MEDIUM → LOW → null)
/// then newest first (OPS-01). Ownership for reads is applied by the service via the
/// <c>isCustomer</c>/<c>callerId</c> arguments.
/// </summary>
public sealed class DisputeRepository(DisputePortalDbContext db) : IDisputeRepository
{
    public Task<Transaction?> GetOwnedTransactionAsync(Guid customerId, Guid transactionId, CancellationToken ct) =>
        db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.CustomerId == customerId, ct);

    public Task<bool> ExistsForTransactionAsync(Guid transactionId, CancellationToken ct) =>
        db.Disputes.AsNoTracking().AnyAsync(d => d.TransactionId == transactionId, ct);

    public Task<int> CountByReferencePrefixAsync(string prefix, CancellationToken ct) =>
        db.Disputes.AsNoTracking().CountAsync(d => d.Reference.StartsWith(prefix), ct);

    public async Task<PagedResult<DisputeSummaryDto>> ListAsync(
        bool isCustomer, Guid callerId, int page, int pageSize,
        DisputeStatus? status, DisputePriority? priority, DisputeCategory? category, CancellationToken ct)
    {
        IQueryable<Dispute> query = db.Disputes.AsNoTracking();

        if (isCustomer) query = query.Where(d => d.CustomerId == callerId);   // row-level security
        if (status is { } s) query = query.Where(d => d.Status == s);
        if (priority is { } p) query = query.Where(d => d.Priority == p);
        if (category is { } c) query = query.Where(d => d.Category == c);

        var total = await query.CountAsync(ct);

        // Priority ordinal (string enum → CASE): CRITICAL > HIGH > MEDIUM > LOW > unclassified.
        var rows = await query
            .OrderByDescending(d => d.Priority == DisputePriority.CRITICAL ? 4
                                  : d.Priority == DisputePriority.HIGH ? 3
                                  : d.Priority == DisputePriority.MEDIUM ? 2
                                  : d.Priority == DisputePriority.LOW ? 1 : 0)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Reference,
                d.TransactionId,
                d.CustomerId,
                CustomerName = isCustomer ? null : d.Customer.FullName,
                d.Status,
                d.Category,
                d.Priority,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new DisputeSummaryDto(
            r.Id, r.Reference, r.TransactionId, r.CustomerId, r.CustomerName,
            r.Status.ToString(), r.Category?.ToString(), r.Priority?.ToString(),
            r.CreatedAt, r.UpdatedAt)).ToList();

        return new PagedResult<DisputeSummaryDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<Dispute?> GetForDetailAsync(Guid id, CancellationToken ct) =>
        db.Disputes.AsNoTracking()
            .Include(d => d.Transaction)
            .Include(d => d.Resolution)
            .Include(d => d.Events).ThenInclude(e => e.Actor)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Dispute?> GetTrackedAsync(Guid id, CancellationToken ct) =>
        db.Disputes
            .Include(d => d.Customer)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Dispute?> GetTrackedForResolveAsync(Guid id, CancellationToken ct) =>
        db.Disputes
            .Include(d => d.Resolution)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
}
