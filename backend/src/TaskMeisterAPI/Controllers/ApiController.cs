using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskMeisterAPI.Controllers;

/// <summary>
/// Base controller that translates ErrorOr errors into consistent HTTP Problem Details responses.
///
/// How to extend:
///   • Add new ErrorType cases to the status-code switch in HandleErrors() when you introduce
///     custom error types via Error.Custom(type, code, description).
///   • Override HandleErrors() in a specific controller if one endpoint needs
///     non-standard status codes for a particular error type.
///   • To add richer Problem Details (e.g. a trace ID or an extensions field),
///     inject IProblemDetailsService and replace the Problem() call below.
/// </summary>
[ApiController]
public abstract class ApiController : ControllerBase
{
    /// <summary>
    /// Maps a list of ErrorOr errors to an IActionResult.
    /// Validation errors are aggregated into a 422 response.
    /// All other errors use the first error's type to determine the status code.
    /// </summary>
    protected IActionResult HandleErrors(List<Error> errors)
    {
        if (errors.Count == 0)
            return Problem();

        // Aggregate validation errors into a 422 with a field-keyed body so
        // clients can display per-field messages.
        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var modelState = new ModelStateDictionary();
            foreach (var e in errors)
                modelState.AddModelError(e.Code, e.Description);
            return ValidationProblem(modelState);
        }

        var first = errors[0];
        return Problem(
            statusCode: first.Type switch
            {
                ErrorType.Validation   => StatusCodes.Status422UnprocessableEntity,
                ErrorType.Conflict     => StatusCodes.Status409Conflict,
                ErrorType.NotFound     => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
                ErrorType.Failure      => StatusCodes.Status400BadRequest,
                _                      => StatusCodes.Status500InternalServerError,
            },
            title:  first.Code,
            detail: first.Description);
    }
}
