namespace DisputePortal.Api.Infrastructure.Exceptions;

/// <summary>
/// Semantic validation failure not covered by DataAnnotations (e.g. an out-of-enum
/// <c>category</c>/<c>status</c>/<c>outcome</c> value) → HTTP 400.
/// </summary>
public sealed class ValidationException(string message) : AppException(message)
{
    public override int StatusCode => 400;
    public override string Title => "Bad Request";
}
