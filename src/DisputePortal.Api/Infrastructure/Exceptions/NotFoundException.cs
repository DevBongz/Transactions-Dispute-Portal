namespace DisputePortal.Api.Infrastructure.Exceptions;

/// <summary>Requested resource does not exist (or is not visible to the caller) → HTTP 404.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;
    public override string Title => "Not Found";
}
