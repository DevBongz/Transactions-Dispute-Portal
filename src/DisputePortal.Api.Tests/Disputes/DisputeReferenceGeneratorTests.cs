using System.Text.RegularExpressions;
using DisputePortal.Api.Services;
using Xunit;

namespace DisputePortal.Api.Tests.Disputes;

/// <summary>
/// Unit tests for <see cref="DisputeReferenceGenerator"/> (TDP-DISP-01 §2.4 / §5 DoD):
/// asserts the <c>DSP-YYYYMMDD-NNNNN</c> format and the per-day increment.
/// </summary>
public sealed class DisputeReferenceGeneratorTests
{
    private static readonly Regex Format = new(@"^DSP-\d{8}-\d{5}$");

    [Fact]
    public async Task Reference_matches_the_DSP_format()
    {
        var repo = new FakeDisputeRepository { CountByPrefix = _ => 0 };
        var gen = new DisputeReferenceGenerator(repo);

        var reference = await gen.GenerateAsync(new DateOnly(2026, 7, 14), CancellationToken.None);

        Assert.Matches(Format, reference);
        Assert.Equal("DSP-20260714-00001", reference);
    }

    [Theory]
    [InlineData(0, "DSP-20260714-00001")]
    [InlineData(41, "DSP-20260714-00042")]
    [InlineData(99998, "DSP-20260714-99999")]
    public async Task Sequence_is_the_days_existing_count_plus_one(int existing, string expected)
    {
        var repo = new FakeDisputeRepository { CountByPrefix = _ => existing };
        var gen = new DisputeReferenceGenerator(repo);

        var reference = await gen.GenerateAsync(new DateOnly(2026, 7, 14), CancellationToken.None);

        Assert.Equal(expected, reference);
    }

    [Fact]
    public async Task Prefix_encodes_the_supplied_date()
    {
        string? captured = null;
        var repo = new FakeDisputeRepository { CountByPrefix = p => { captured = p; return 0; } };
        var gen = new DisputeReferenceGenerator(repo);

        await gen.GenerateAsync(new DateOnly(2026, 12, 1), CancellationToken.None);

        Assert.Equal("DSP-20261201-", captured);
    }
}
