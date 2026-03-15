using FinanceTracker.Application.Common;

namespace FinanceTracker.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApplicationExceptionBase ex) when (ex is ValidationException or ConflictException or NotFoundException)
        {
            await WriteKnownProblemAsync(context, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing request {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Unexpected server error",
                Detail = "The request could not be completed.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    private static Task WriteKnownProblemAsync(HttpContext context, ApplicationExceptionBase ex)
    {
        var (statusCode, title) = ex switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (StatusCodes.Status400BadRequest, "Request error")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Detail = ex.Message,
            Status = statusCode
        });
    }
}