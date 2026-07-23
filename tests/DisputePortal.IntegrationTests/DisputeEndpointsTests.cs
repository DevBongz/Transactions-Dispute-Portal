using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DisputePortal.IntegrationTests;

[Collection("api")]
public sealed class DisputeEndpointsTests
{
    private readonly DisputePortalApiFactory _factory;

    public DisputeEndpointsTests(DisputePortalApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostDisputes_WithValidTransaction_Returns201WithReferenceAndPublishesSubmittedEvent()
    {
        var (customerId, txnId) = await _factory.SeedCustomerWithTransactionAsync(amount: 450m);
        var client = _factory.CreateClientAs(customerId, "CUSTOMER");

        var before = _factory.Events.Published.Count;
        var resp = await client.PostAsJsonAsync("/api/v1/disputes", new
        {
            transactionId = txnId,
            description = "Charged R450 twice at Shoprite on 14 July."
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        root.GetProperty("reference").GetString().Should().MatchRegex(@"^DSP-\d{8}-\d{5}$");
        root.GetProperty("status").GetString().Should().Be("OPEN");

        _factory.Events.Published.Skip(before)
            .Should().Contain(e => e.Topic == "dispute.submitted");
    }

    [Fact]
    public async Task PostDisputes_WithoutJwt_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/disputes",
            new { transactionId = Guid.NewGuid(), description = "missing auth should fail" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
