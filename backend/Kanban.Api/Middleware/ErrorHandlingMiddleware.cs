using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Kanban.Api.Exceptions;

namespace Kanban.Api.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestId = context.TraceIdentifier;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? "anonymous";

        _logger.LogError(
            exception,
            "Unhandled exception. UserId: {UserId}, RequestId: {RequestId}, Method: {Method}, Path: {Path}",
            userId,
            requestId,
            context.Request.Method,
            context.Request.Path.Value);

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, the error middleware will not write a response.");
            return;
        }

        var (statusCode, errorCode, message, validationErrors) = MapException(exception);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = new
            {
                code = errorCode,
                message,
                timestamp = DateTimeOffset.UtcNow,
                requestId,
                validationErrors
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static (int StatusCode, string ErrorCode, string Message, object? ValidationErrors) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Validation failed.",
                validationException.Errors.Select(error => new
                {
                    propertyName = error.PropertyName,
                    errorMessage = error.ErrorMessage,
                    attemptedValue = error.AttemptedValue
                }).ToArray()),
            BadRequestException badRequestException => (
                StatusCodes.Status400BadRequest,
                badRequestException.Code ?? "BAD_REQUEST",
                exception.Message,
                null),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                exception.Message,
                null),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                exception.Message,
                null),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                exception.Message,
                null),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                exception.Message,
                null),
            ConflictException conflictException => (
                StatusCodes.Status409Conflict,
                conflictException.Code,
                exception.Message,
                null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.",
                null)
        };
    }
}
