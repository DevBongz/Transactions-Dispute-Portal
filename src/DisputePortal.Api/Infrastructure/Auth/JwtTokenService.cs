using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DisputePortal.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace DisputePortal.Api.Infrastructure.Auth;

/// <summary>
/// Issues self-contained HS256 JWTs (TDP-AUTH-01 §2.3). The token carries the
/// user id (<c>sub</c>), email, a unique <c>jti</c>, full name, and the role as
/// <see cref="ClaimTypes.Role"/> so ASP.NET Core role policies work out of the box.
/// Expiry is <see cref="JwtOptions.ExpiryMinutes"/> (60 min, AC-AUTH-01).
/// </summary>
public sealed class JwtTokenService(JwtOptions options)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())   // CUSTOMER | OPS_ANALYST | OPS_MANAGER
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
