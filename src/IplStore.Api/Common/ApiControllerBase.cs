using IplStore.Shared;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Common;

/// <summary>
/// Base controller that translates the domain Result/Error pattern into HTTP responses
/// using RFC 7807 ProblemDetails. Keeps controllers thin and mapping consistent.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult(Result result)
        => result.IsSuccess ? NoContent() : Problem(result.Error);

    protected IActionResult HandleResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : Problem(result.Error);

    protected IActionResult HandleCreated<T>(Result<T> result, string routeName, Func<T, object> routeValues)
        => result.IsSuccess
            ? CreatedAtRoute(routeName, routeValues(result.Value), result.Value)
            : Problem(result.Error);

    protected IActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            title: ReasonPhrase(statusCode),
            detail: error.Description,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Server Error"
    };
}
