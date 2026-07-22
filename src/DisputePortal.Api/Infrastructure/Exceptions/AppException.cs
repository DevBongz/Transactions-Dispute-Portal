namespace DisputePortal.Api.Infrastructure.Exceptions;

/// <summary>
/// Base type for expected, client-facing application errors that map to a specific
/// HTTP status via <see cref="ExceptionHandlingMiddleware"/> (TDP-DISP-01 §2.6). Using
/// exceptions keeps the service layer free of HTTP concerns while still surfacing
/// precise status codes (404 / 409 / 400) at the edge.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }

    public abstract int StatusCode { get; }
    public abstract string Title { get; }
}
