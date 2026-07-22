using System.Text.Json;
using DisputePortal.Api.Services.Ai;
using Microsoft.AspNetCore.Mvc;


namespace DisputePortal.Api.Infrastructure.Exceptions;

/// <summary>
/// Terminal error boundary that translates <see cref="AppException"/>s into
/// RFC 7807 <c>application/problem+json</c> responses with the mapped status
/// (404 / 409 / 400) and everything else into a 500 (TDP-DISP-01 §2.6). Registered
/// early in the pipeline so it wraps controller execution; correlation-id logging
/// remains upstream so failures are still correlated.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogInformation(
                "Request {Method} {Path} rejected: {Status} {Title} — {Detail}",
                context.Request.Method, context.Request.Path, ex.StatusCode, ex.Title, ex.Message);
            await WriteProblemAsync(context, ex.StatusCode, ex.Title, ex.Message);
        }
        catch (AnthropicException ex)
        {
            // Defence in depth if a controller forgets to map LLM failures to 502.
            logger.LogWarning(ex, "LLM call failed for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status502BadGateway,
                "Bad Gateway", "AI service temporarily unavailable.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, Json));
    }
}
