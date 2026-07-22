using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Domain;

namespace DisputePortal.Api.Repositories;

/// <summary>
/// Data access for disputes (TDP-DISP-01/02/03). Read queries are <c>AsNoTracking</c> with
/// ownership enforced inside the query; write helpers return tracked entities for the
/// service to mutate within a unit of work. Enum filters are pre-parsed/validated by the
/// service (unknown values never reach here).
/// </summary>
public interface IDisputeRepository
{
    /// <summary>The transaction if it exists AND belongs to <paramref name="customerId"/>, else null.</summary>
    Task<Transaction?> GetOwnedTransactionAsync(Guid customerId, Guid transactionId, CancellationToken ct);

    /// <summary>True if a dispute already exists for the transaction (1:1 guard, SPEC §3.2).</summary>
    Task<bool> ExistsForTransactionAsync(Guid transactionId, CancellationToken ct);

    /// <summary>Count of disputes whose reference starts with <paramref name="prefix"/> (per-day sequence).</summary>
    Task<int> CountByReferencePrefixAsync(string prefix, CancellationToken ct);

    /// <summary>Role-scoped, filtered, priority-ordered dispute list.</summary>
    Task<PagedResult<DisputeSummaryDto>> ListAsync(
        bool isCustomer, Guid callerId, int page, int pageSize,
        DisputeStatus? status, DisputePriority? priority, DisputeCategory? category, CancellationToken ct);

    /// <summary>Dispute with Transaction, Resolution and Events(+Actor) for the detail view; null if not found.</summary>
    Task<Dispute?> GetForDetailAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked dispute (with Customer) for a status transition; null if not found.</summary>
    Task<Dispute?> GetTrackedAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked dispute (with Resolution) for resolution; null if not found.</summary>
    Task<Dispute?> GetTrackedForResolveAsync(Guid id, CancellationToken ct);
}
