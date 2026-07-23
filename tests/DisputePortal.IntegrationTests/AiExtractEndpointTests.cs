using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DisputePortal.IntegrationTests;

[Collection("api")]
public sealed class AiExtractEndpointTests
{
    private readonly DisputePortalApiFactory _factory;

    public AiExtractEndpointTests(DisputePortalApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExtractDispute_ReturnsStructuredFieldsAndConfidenceMap()
    {
        var client = _factory.CreateClientAs(Guid.NewGuid(), "CUSTOMER");

        var resp = await client.PostAsJsonAsync("/api/v1/ai/extract-dispute",
            new { text = "I was charged R450 twice at Shoprite on 14 July but I only shopped once." });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        root.GetProperty("merchantName").GetString().Should().Be("Shoprite");
        root.GetProperty("amount").GetDecimal().Should().Be(450m);
        root.GetProperty("category").GetString().Should().Be("DUPLICATE_CHARGE");
        root.GetProperty("confidence").EnumerateObject().Should().NotBeEmpty();
    }
}
