namespace DisputePortal.Api.Infrastructure.Exceptions;

/// <summary>Request conflicts with current state (duplicate dispute, illegal transition, already resolved) → HTTP 409.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;
    public override string Title => "Conflict";
}
