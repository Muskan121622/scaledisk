using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScaleDesk.Application.Exceptions;

namespace ScaleDesk.Api.Middleware;

/// <summary>
/// Single translation layer from thrown exceptions to RFC-7807 ProblemDetails. Expected
/// application failures map to 4xx with a stable error code; anything unexpected becomes a
/// sanitized 500 so internal details never reach clients (spec §42 error sanitization).
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, code, title, errors) = exception switch
        {
            ValidationException v => (StatusCodes.Status400BadRequest, v.ErrorCode, v.Message, v.Errors),
            NotFoundException n => (StatusCodes.Status404NotFound, n.ErrorCode, n.Message, null),
            ForbiddenException f => (StatusCodes.Status403Forbidden, f.ErrorCode, f.Message, null),
            TenantNotResolvedException t => (StatusCodes.Status400BadRequest, t.ErrorCode, t.Message, null),
            ConflictException c => (StatusCodes.Status409Conflict, c.ErrorCode, c.Message, null),
            ConcurrencyException ce => (StatusCodes.Status409Conflict, ce.ErrorCode, ce.Message, null),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "conflict_concurrency",
                "The record was modified by another user. Reload and try again.", null),
            _ => (StatusCodes.Status500InternalServerError, "error", "An unexpected error occurred.", null)
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
        else
            logger.LogWarning("Handled {Code} for {Path}: {Title}", code, context.Request.Path, title);

        // Echo the request's correlation id (set by RequestContextMiddleware) so a client-reported
        // problem can be matched to its exact server-side log trace.
        var correlationId = context.Items[RequestContextMiddleware.ItemKey] as string;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://scaledesk.dev/errors/{code}",
            Detail = exception is ValidationException ? title : null,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        if (correlationId is not null)
            problem.Extensions["correlationId"] = correlationId;
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
