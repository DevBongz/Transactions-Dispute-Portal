namespace DisputePortal.Api.Infrastructure.Auth;

/// <summary>
/// Strongly-typed JWT configuration bound from the <c>Jwt</c> config section
/// (TDP-AUTH-01 §2.1). The <see cref="Secret"/> is injected from the environment
/// (compose sets <c>Jwt__Secret</c> from <c>JWT_SECRET</c>) and is validated
/// fail-fast at startup.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Secret { get; set; } = default!;
    public int ExpiryMinutes { get; set; } = 60;
}
