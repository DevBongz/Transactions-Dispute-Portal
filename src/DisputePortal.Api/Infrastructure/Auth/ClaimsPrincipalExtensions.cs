using System.Security.Claims;

namespace DisputePortal.Api.Infrastructure.Auth;

/// <summary>
/// Helpers for reading identity off the authenticated principal (TDP-AUTH-01 /
/// TDP-TXN-01 §2.5). The JWT <c>sub</c> claim is surfaced as
/// <see cref="ClaimTypes.NameIdentifier"/> because inbound claim mapping is left on
/// (Batch 2), so the caller's user id is read from there.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Resolves the authenticated caller's user id from the <c>sub</c> claim.</summary>
    /// <exception cref="InvalidOperationException">No usable id claim is present.</exception>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(raw, out var id))
            return id;

        throw new InvalidOperationException("Authenticated principal has no valid user id claim.");
    }
}
