using DisputePortal.Api.Data;
using DisputePortal.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Controllers;

/// <summary>
/// Authentication endpoints (TDP-AUTH-01 §2.4, SPEC §3.3). <c>login</c> is public;
/// everything else in the API sits behind the global authenticated fallback policy.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(DisputePortalDbContext db, JwtTokenService tokens) : ControllerBase
{
    /// <summary>Authenticate with email + password and receive a 60-minute JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == req.Email);

        // Single generic failure path — do not reveal whether the email exists
        // (no credential enumeration, AC-AUTH-01).
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var (token, expiresAt) = tokens.Create(user);
        return Ok(new LoginResponse(token, expiresAt,
            new UserDto(user.Id, user.FullName, user.Role.ToString())));
    }

    /// <summary>Stateless logout — the client discards the token (SPEC §3.3).</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout() => NoContent();
}

public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(Guid Id, string FullName, string Role);
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
