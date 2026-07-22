using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Infrastructure.Auth;
using Xunit;

namespace DisputePortal.Api.Tests.Auth;

/// <summary>
/// Focused unit tests for token issuance + password verification (TDP-AUTH-01 §5,
/// feeds TDP-TEST-01's "JWT middleware" cases, SPEC §4.4).
/// </summary>
public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "dispute-portal",
        Audience = "dispute-portal-clients",
        Secret = "test-secret-that-is-at-least-32-bytes-long!!",
        ExpiryMinutes = 60
    };

    private static User NewUser(UserRole role = UserRole.CUSTOMER) => new()
    {
        Id = Guid.NewGuid(),
        Email = "maya@example.com",
        FullName = "Maya Naidoo",
        PasswordHash = "n/a",
        Role = role,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Create_issues_token_expiring_in_60_minutes()
    {
        var svc = new JwtTokenService(Options);

        var (token, expiresAt) = svc.Create(NewUser());

        Assert.False(string.IsNullOrWhiteSpace(token));
        var minutes = (expiresAt - DateTimeOffset.UtcNow).TotalMinutes;
        Assert.InRange(minutes, 59, 60.1);
    }

    [Fact]
    public void Create_embeds_sub_email_and_role_claims()
    {
        var user = NewUser(UserRole.OPS_MANAGER);
        var svc = new JwtTokenService(Options);

        var (token, _) = svc.Create(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Options.Issuer, jwt.Issuer);
        Assert.Contains(Options.Audience, jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        // ClaimTypes.Role serialises to the short "role" claim in the JWT payload.
        Assert.Equal(nameof(UserRole.OPS_MANAGER), jwt.Claims.Single(c => c.Type == ClaimTypes.Role || c.Type == "role").Value);
    }

    [Theory]
    [InlineData("Password123!", true)]
    [InlineData("wrong-password", false)]
    public void BCrypt_verify_matches_only_the_correct_password(string attempt, bool expected)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 12);

        Assert.Equal(expected, BCrypt.Net.BCrypt.Verify(attempt, hash));
    }
}
