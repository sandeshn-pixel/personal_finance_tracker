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
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    private static Task WriteKnownProblemAsync(HttpContext context, ApplicationExceptionBase ex)
    {
        var statusCode = ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            ConflictException => StatusCodes.Status409Conflict,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = ex.GetType().Name,
            Detail = ex.Message,
            Status = statusCode
        });
    }
}
