using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DisputePortal.Api.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DisputePortal.Api.Tests.Auth;

/// <summary>
/// Exercises the real JwtBearer pipeline (valid / expired / missing) per TDP-TEST-01 §2.5.
/// </summary>
public sealed class JwtMiddlewareTests
{
    private const string Secret = "test-secret-that-is-at-least-32-bytes-long!!";

    private static HttpClient BuildClient()
    {
        var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(s =>
            {
                s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(o =>
                    {
                        o.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = "dispute-portal",
                            ValidateAudience = true,
                            ValidAudience = "dispute-portal-clients",
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero
                        };
                    });
                s.AddAuthorization();
                s.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(e =>
                    e.MapGet("/secure", async ctx =>
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.WriteAsync("ok");
                    }).RequireAuthorization());
            }));
        return server.CreateClient();
    }

    private static string Mint(DateTime expiresUtc)
    {
        var opts = new JwtOptions
        {
            Issuer = "dispute-portal",
            Audience = "dispute-portal-clients",
            Secret = Secret,
            ExpiryMinutes = 60
        };
        // Build via handler so we can force an arbitrary expiry for the expired-token case.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: [new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString())],
            expires: expiresUtc,
            signingCredentials: creds);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task ValidToken_Returns200()
    {
        var client = BuildClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Mint(DateTime.UtcNow.AddMinutes(30)));
        (await client.GetAsync("/secure")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var client = BuildClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Mint(DateTime.UtcNow.AddMinutes(-1)));
        (await client.GetAsync("/secure")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingToken_Returns401() =>
        (await BuildClient().GetAsync("/secure")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
