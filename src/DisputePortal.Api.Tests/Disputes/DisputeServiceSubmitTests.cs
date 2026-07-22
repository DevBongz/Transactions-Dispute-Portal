using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Exceptions;
using DisputePortal.Api.Messaging;
using DisputePortal.Api.Messaging.Events;
using DisputePortal.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DisputePortal.Api.Tests.Disputes;

/// <summary>
/// Unit tests for <see cref="DisputeService.SubmitDisputeAsync"/> guard rails
/// (TDP-DISP-01 §5 DoD): ownership (404), duplicate-dispute (409), and out-of-enum
/// category (400). These paths all short-circuit before any DB write, so a
/// never-connected DbContext is sufficient.
/// </summary>
public sealed class DisputeServiceSubmitTests
{
    private sealed class NoOpPublisher : IEventPublisher
    {
        public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default) => Task.CompletedTask;
    }

    // A DbContext that is constructed but never used (guards throw first).
    private static DisputePortalDbContext UnusedDb() =>
        new(new DbContextOptionsBuilder<DisputePortalDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=u;Password=p").Options);

    private static DisputeService ServiceWith(FakeDisputeRepository repo) =>
        new(UnusedDb(), repo, new DisputeReferenceGenerator(repo), new NoOpPublisher());

    private static SubmitDisputeRequest Request(string? category = null) =>
        new(Guid.NewGuid(), category, "This is a valid dispute description.", null);

    [Fact]
    public async Task Unowned_or_missing_transaction_throws_NotFound()
    {
        var repo = new FakeDisputeRepository { OwnedTransaction = null };
        var service = ServiceWith(repo);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SubmitDisputeAsync(Guid.NewGuid(), Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Existing_dispute_for_transaction_throws_Conflict()
    {
        var repo = new FakeDisputeRepository
        {
            OwnedTransaction = new Transaction { Id = Guid.NewGuid() },
            ExistsForTransaction = true
        };
        var service = ServiceWith(repo);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.SubmitDisputeAsync(Guid.NewGuid(), Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Out_of_enum_category_throws_Validation()
    {
        var repo = new FakeDisputeRepository { OwnedTransaction = new Transaction { Id = Guid.NewGuid() } };
        var service = ServiceWith(repo);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SubmitDisputeAsync(Guid.NewGuid(), Request(category: "NOPE"), CancellationToken.None));
    }
}
