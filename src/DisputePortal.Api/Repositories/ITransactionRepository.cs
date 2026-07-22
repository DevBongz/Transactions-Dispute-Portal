using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;

namespace DisputePortal.Api.Repositories;

/// <summary>
/// Data access for transactions (TDP-TXN-01 §2.6). Ownership is enforced inside the
/// query (<c>WHERE customer_id = @callerId</c>) — never after materialisation — so it
/// doubles as the security control and keeps SQL injection off the table via EF Core
/// parameterisation (SPEC §3.6). The query is assumed pre-validated and normalised by
/// the service layer.
/// </summary>
public interface ITransactionRepository
{
    Task<PagedResult<TransactionDto>> ListAsync(Guid customerId, TransactionQuery query, CancellationToken ct);
    Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct);
}
