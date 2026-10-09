using LogisticServer.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LogisticServer.Infrastructure.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString() 
            ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException vex => (
                StatusCodes.Status400BadRequest, 
                "Validation Error", 
                vex.Message, 
                vex.Errors.Count > 0 ? vex.Errors : null),

            ArgumentException aex => (
                StatusCodes.Status400BadRequest, 
                "Bad Request", 
                aex.Message, 
                null),

            UnauthorizedException uex => (
                StatusCodes.Status401Unauthorized, 
                "Unauthorized", 
                uex.Message, 
                null),

            ForbiddenException fex => (
                StatusCodes.Status403Forbidden, 
                "Forbidden", 
                fex.Message, 
                null),

            NotFoundException nex => (
                StatusCodes.Status404NotFound, 
                "Not Found", 
                nex.Message, 
                null),

            ConflictException cex => (
                StatusCodes.Status409Conflict, 
                "Conflict", 
                cex.Message, 
                null),

            _ => (
                StatusCodes.Status500InternalServerError, 
                "Internal Server Error", 
                "An unexpected error occurred while processing your request.", 
                null)
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "[{CorrelationId}] Unhandled exception on path {Path}", correlationId, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("[{CorrelationId}] Handled exception ({StatusCode} {Title}) on path {Path}: {Detail}", 
                correlationId, statusCode, title, httpContext.Request.Path, detail);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (errors != null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}

