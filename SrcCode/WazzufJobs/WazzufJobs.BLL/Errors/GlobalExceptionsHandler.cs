using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WazzufJobs.BLL.Errors;

public class GlobalExceptionsHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionsHandler> _logger;

    public GlobalExceptionsHandler(ILogger<GlobalExceptionsHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            // Phase 02 S1: RFC 9457 validation response. Field names and messages only - attempted values are never
            // returned or logged. "detail" carries the first message so existing clients that read "detail" show it.
            var errors = ValidationErrors.ToDictionary(validationException.Errors);
            _logger.LogInformation("Request validation failed for {Path}; fields: {Fields}.",
                httpContext.Request.Path.Value, string.Join(", ", errors.Keys));

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Type = ValidationErrors.Type,
                Title = ValidationErrors.Title,
                Status = StatusCodes.Status400BadRequest,
                Detail = validationException.Errors.First().ErrorMessage
            };
            validationProblem.Extensions["code"] = ValidationErrors.Code;
            validationProblem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, validationProblem.GetType(),
                options: null, contentType: "application/problem+json", cancellationToken);

            return true;
        }

        _logger.LogError(exception, "Something Went Wrong: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500"
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}