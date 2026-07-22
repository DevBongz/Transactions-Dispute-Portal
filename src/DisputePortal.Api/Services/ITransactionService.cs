using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;

namespace DisputePortal.Api.Services;

/// <summary>
/// Application service for transaction reads (TDP-TXN-01). Owns query
/// validation/normalisation (page/pageSize clamping, inclusive date-boundary
/// handling) and delegates data access to <see cref="Repositories.ITransactionRepository"/>.
/// </summary>
public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> ListAsync(Guid customerId, TransactionQuery query, CancellationToken ct);
    Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct);
}
