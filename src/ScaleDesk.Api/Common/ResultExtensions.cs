using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Application.Common;

namespace ScaleDesk.Api.Common;

/// <summary>
/// Maps an Application <see cref="Result"/> into an HTTP response, translating stable error codes
/// into the right status. Keeps controllers thin and the failure vocabulary consistent.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : result.ToError();

    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new NoContentResult()
            : result.ToError();

    private static IActionResult ToError<T>(this Result<T> result) => ToErrorCore(result.ErrorCode, result.Message);
    private static IActionResult ToError(this Result result) => ToErrorCore(result.ErrorCode, result.Message);

    private static IActionResult ToErrorCore(string? code, string? message)
    {
        var status = code switch
        {
            "invalid_credentials" => StatusCodes.Status401Unauthorized,
            "invalid_refresh" or "expired_refresh" => StatusCodes.Status401Unauthorized,
            "locked_out" => StatusCodes.Status423Locked,
            "tenant_suspended" => StatusCodes.Status403Forbidden,
            "email_taken" or "slug_taken" => StatusCodes.Status409Conflict,
            "not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = message,
            Type = $"https://scaledesk.dev/errors/{code}"
        })
        { StatusCode = status };
    }
}
