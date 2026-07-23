using System.Net;
using System.Net.Http.Json;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DisputePortal.IntegrationTests;

[Collection("api")]
public sealed class ResolveEndpointTests
{
    private readonly DisputePortalApiFactory _factory;

    public ResolveEndpointTests(DisputePortalApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Resolve_AsOpsAnalyst_PersistsResolutionAndPublishesResolvedEvent()
    {
        var (disputeId, _, opsId) = await _factory.SeedOpenDisputeAsync();
        var client = _factory.CreateClientAs(opsId, "OPS_ANALYST");
        var before = _factory.Events.Published.Count;

        var resp = await client.PostAsJsonAsync($"/api/v1/disputes/{disputeId}/resolve", new
        {
            outcome = "UPHELD",
            internalNotes = "Transaction confirmed as duplicate — refund initiated via settlement-processor.",
            customerSummary = "We reviewed your dispute and refunded the duplicate R450 charge."
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DisputePortalDbContext>();
        var dispute = await db.Disputes.Include(d => d.Resolution)
            .SingleAsync(d => d.Id == disputeId);
        dispute.Status.Should().Be(DisputeStatus.RESOLVED);
        dispute.Resolution!.Outcome.Should().Be(ResolutionOutcome.UPHELD);

        _factory.Events.Published.Skip(before)
            .Should().Contain(e => e.Topic == "dispute.resolved");
    }

    [Fact]
    public async Task Resolve_AsCustomer_Returns403()
    {
        var (disputeId, customerId, _) = await _factory.SeedOpenDisputeAsync();
        var client = _factory.CreateClientAs(customerId, "CUSTOMER");

        var resp = await client.PostAsJsonAsync($"/api/v1/disputes/{disputeId}/resolve", new
        {
            outcome = "UPHELD",
            internalNotes = new string('x', 25),
            customerSummary = "summary"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
