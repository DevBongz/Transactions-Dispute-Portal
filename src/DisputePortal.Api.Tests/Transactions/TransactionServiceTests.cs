using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;
using DisputePortal.Api.Repositories;
using DisputePortal.Api.Services;
using Xunit;

namespace DisputePortal.Api.Tests.Transactions;

/// <summary>
/// Unit tests for the query clamp/normalisation logic in <see cref="TransactionService"/>
/// (TDP-TXN-01 §2.4 DoD). A capturing fake repository records the normalised query the
/// service forwards, so we assert paging guard rails and inclusive date-boundary handling
/// without a database.
/// </summary>
public sealed class TransactionServiceTests
{
    private sealed class CapturingRepository : ITransactionRepository
    {
        public TransactionQuery? Captured { get; private set; }

        public Task<PagedResult<TransactionDto>> ListAsync(Guid customerId, TransactionQuery query, CancellationToken ct)
        {
            Captured = query;
            return Task.FromResult(new PagedResult<TransactionDto>());
        }

        public Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct) =>
            Task.FromResult<TransactionDto?>(null);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public async Task Page_is_coerced_to_at_least_one(int input, int expected)
    {
        var repo = new CapturingRepository();
        var svc = new TransactionService(repo);

        await svc.ListAsync(Guid.NewGuid(), new TransactionQuery { Page = input }, CancellationToken.None);

        Assert.Equal(expected, repo.Captured!.Page);
    }

    [Theory]
    [InlineData(0, 20)]     // non-positive falls back to default 20
    [InlineData(50, 50)]
    [InlineData(500, 100)]  // clamped to max 100
    public async Task PageSize_is_clamped(int input, int expected)
    {
        var repo = new CapturingRepository();
        var svc = new TransactionService(repo);

        await svc.ListAsync(Guid.NewGuid(), new TransactionQuery { PageSize = input }, CancellationToken.None);

        Assert.Equal(expected, repo.Captured!.PageSize);
    }

    [Fact]
    public async Task Date_only_To_is_normalised_to_end_of_day_for_inclusive_boundary()
    {
        var repo = new CapturingRepository();
        var svc = new TransactionService(repo);
        var to = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        await svc.ListAsync(Guid.NewGuid(), new TransactionQuery { To = to }, CancellationToken.None);

        var normalised = repo.Captured!.To!.Value;
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 23, 59, 59, TimeSpan.Zero).Date, normalised.Date);
        Assert.Equal(23, normalised.Hour);
        Assert.Equal(59, normalised.Minute);
        Assert.Equal(59, normalised.Second);
    }

    [Fact]
    public async Task Timestamped_To_is_left_untouched()
    {
        var repo = new CapturingRepository();
        var svc = new TransactionService(repo);
        var to = new DateTimeOffset(2026, 7, 14, 10, 22, 0, TimeSpan.Zero);

        await svc.ListAsync(Guid.NewGuid(), new TransactionQuery { To = to }, CancellationToken.None);

        Assert.Equal(to, repo.Captured!.To);
    }
}
