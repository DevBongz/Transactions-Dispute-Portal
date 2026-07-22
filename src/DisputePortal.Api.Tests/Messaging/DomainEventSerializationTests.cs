using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DisputePortal.Api.Messaging.Events;
using Xunit;

namespace DisputePortal.Api.Tests.Messaging;

/// <summary>
/// Asserts the three domain events serialise to the exact field set/shape in
/// SPEC §3.4 (TDP-KAFKA-01 §2.3 DoD): camelCase names, explicit <c>"category": null</c>
/// on submission, envelope-first field order, and no routing metadata leaking into
/// the payload. Uses the same JSON options as <c>KafkaEventPublisher</c>.
/// </summary>
public sealed class DomainEventSerializationTests
{
    // Mirrors KafkaEventPublisher.Json.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static string Serialize(IDomainEvent e) => JsonSerializer.Serialize(e, e.GetType(), Json);

    [Fact]
    public void DisputeSubmitted_matches_spec_field_set_with_explicit_null_category()
    {
        var e = new DisputeSubmittedEvent(
            DisputeId: Guid.NewGuid(),
            Reference: "DSP-20260714-00042",
            TransactionId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Category: null,
            Description: "I was charged R450 twice at Shoprite.");

        var node = JsonNode.Parse(Serialize(e))!.AsObject();

        Assert.Equal(
            new[] { "eventId", "occurredAt", "disputeId", "reference", "transactionId", "customerId", "category", "description" },
            node.Select(p => p.Key).ToArray());

        // category present and explicitly null (not omitted).
        Assert.True(node.ContainsKey("category"));
        Assert.Null(node["category"]);
        // Routing metadata must NOT be in the payload.
        Assert.False(node.ContainsKey("topic"));
        Assert.False(node.ContainsKey("partitionKey"));
    }

    [Fact]
    public void DisputeClassified_matches_spec_field_set()
    {
        var e = new DisputeClassifiedEvent(
            DisputeId: Guid.NewGuid(),
            Reference: "DSP-20260714-00042",
            Category: "DUPLICATE_CHARGE",
            Priority: "HIGH",
            ClassifiedBy: "claude-haiku-4-5-20251001");

        var node = JsonNode.Parse(Serialize(e))!.AsObject();

        Assert.Equal(
            new[] { "eventId", "occurredAt", "disputeId", "reference", "category", "priority", "classifiedBy" },
            node.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void DisputeResolved_matches_spec_field_set()
    {
        var e = new DisputeResolvedEvent(
            DisputeId: Guid.NewGuid(),
            Reference: "DSP-20260714-00042",
            Outcome: "UPHELD",
            ResolvedById: Guid.NewGuid(),
            CustomerSummaryProvided: true);

        var node = JsonNode.Parse(Serialize(e))!.AsObject();

        Assert.Equal(
            new[] { "eventId", "occurredAt", "disputeId", "reference", "outcome", "resolvedById", "customerSummaryProvided" },
            node.Select(p => p.Key).ToArray());
        Assert.True(node["customerSummaryProvided"]!.GetValue<bool>());
    }

    [Fact]
    public void All_events_are_keyed_by_disputeId_for_ordering()
    {
        var id = Guid.NewGuid();
        var submitted = new DisputeSubmittedEvent(id, "r", Guid.NewGuid(), Guid.NewGuid(), null, "d");

        Assert.Equal(id.ToString(), submitted.PartitionKey);
        Assert.Equal("dispute.submitted", submitted.Topic);
    }
}
