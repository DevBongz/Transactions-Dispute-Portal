using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITransactionRepository"/> (TDP-TXN-01 §2.6).
/// All reads are <c>AsNoTracking</c> with DTO projection to avoid over-fetching, and
/// ordered by <c>transaction_date DESC</c> then <c>id</c> for deterministic paging.
/// </summary>
public sealed class TransactionRepository(DisputePortalDbContext db) : ITransactionRepository
{
    public async Task<PagedResult<TransactionDto>> ListAsync(
        Guid customerId, TransactionQuery q, CancellationToken ct)
    {
        var query = Filtered(customerId, q);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(Project())
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>
        {
            Items = items,
            Total = total,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    public Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct) =>
        db.Transactions.AsNoTracking()
            .Where(t => t.Id == id && t.CustomerId == customerId)
            .Select(Project())
            .FirstOrDefaultAsync(ct);

    private IQueryable<Transaction> Filtered(Guid customerId, TransactionQuery q)
    {
        IQueryable<Transaction> query = db.Transactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId);

        if (q.From is { } from) query = query.Where(t => t.TransactionDate >= from);
        if (q.To is { } to) query = query.Where(t => t.TransactionDate <= to);   // inclusive (AC-TXN-01)
        if (!string.IsNullOrWhiteSpace(q.Merchant))
            query = query.Where(t => EF.Functions.ILike(t.MerchantName, $"%{q.Merchant}%"));

        return query;
    }

    // Shared projection — HasDispute via correlated existence subquery (no join fan-out).
    private System.Linq.Expressions.Expression<Func<Transaction, TransactionDto>> Project() =>
        t => new TransactionDto(
            t.Id,
            t.Reference,
            t.MerchantName,
            t.MerchantCategory,
            t.Amount,
            t.Currency,
            t.TransactionDate,
            t.Status.ToString(),
            db.Disputes.Any(d => d.TransactionId == t.Id));
}
