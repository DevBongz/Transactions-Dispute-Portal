using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Repositories;

namespace DisputePortal.Api.Tests.Disputes;

/// <summary>
/// Configurable in-memory <see cref="IDisputeRepository"/> for service/generator unit tests
/// (TDP-DISP-01 §5). Only the members exercised by a given test need be configured; the rest
/// throw so accidental reliance on unconfigured behaviour is loud.
/// </summary>
internal sealed class FakeDisputeRepository : IDisputeRepository
{
    public Func<string, int>? CountByPrefix { get; init; }
    public Transaction? OwnedTransaction { get; init; }
    public bool ExistsForTransaction { get; init; }

    /// <summary>Dispute returned by <see cref="GetForDetailAsync"/> (used by AI-03 summary tests).</summary>
    public Dispute? Detail { get; init; }

    public Task<Transaction?> GetOwnedTransactionAsync(Guid customerId, Guid transactionId, CancellationToken ct) =>
        Task.FromResult(OwnedTransaction);

    public Task<bool> ExistsForTransactionAsync(Guid transactionId, CancellationToken ct) =>
        Task.FromResult(ExistsForTransaction);

    public Task<int> CountByReferencePrefixAsync(string prefix, CancellationToken ct) =>
        Task.FromResult((CountByPrefix ?? throw new InvalidOperationException("CountByPrefix not configured"))(prefix));

    public Task<PagedResult<DisputeSummaryDto>> ListAsync(
        bool isCustomer, Guid callerId, int page, int pageSize,
        DisputeStatus? status, DisputePriority? priority, DisputeCategory? category, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Dispute?> GetForDetailAsync(Guid id, CancellationToken ct) => Task.FromResult(Detail);
    public Task<Dispute?> GetTrackedAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Dispute?> GetTrackedForResolveAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Dispute?> GetTrackedForClassificationAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<int> CountOpenForCustomerAsync(Guid customerId, Guid excludeDisputeId, CancellationToken ct) => throw new NotSupportedException();
}
