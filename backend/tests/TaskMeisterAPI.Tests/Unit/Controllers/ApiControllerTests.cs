using ErrorOr;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using TaskMeisterAPI.Controllers;

namespace TaskMeisterAPI.Tests.Unit.Controllers;

/// <summary>
/// Concrete implementation that exposes the protected HandleErrors method for testing.
/// </summary>
public class TestableApiController : ApiController
{
    public IActionResult InvokeHandleErrors(List<Error> errors) => HandleErrors(errors);
}

public class ApiControllerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TestableApiController CreateController()
    {
        // Supply a ProblemDetailsFactory so Problem() and ValidationProblem()
        // can build their response objects.
        var mockFactory = Substitute.For<ProblemDetailsFactory>();

        mockFactory
            .CreateProblemDetails(
                Arg.Any<HttpContext>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns(callInfo => new ProblemDetails
            {
                Status = callInfo.ArgAt<int?>(1) ?? 500,
                Title  = callInfo.ArgAt<string?>(2),
                Detail = callInfo.ArgAt<string?>(4),
            });

        mockFactory
            .CreateValidationProblemDetails(
                Arg.Any<HttpContext>(),
                Arg.Any<ModelStateDictionary>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns(callInfo => new ValidationProblemDetails(callInfo.ArgAt<ModelStateDictionary>(1))
            {
                Status = callInfo.ArgAt<int?>(2) ?? 422,
            });

        var controller = new TestableApiController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.ProblemDetailsFactory = mockFactory;
        return controller;
    }

    private static int StatusOf(IActionResult result) =>
        ((ObjectResult)result).StatusCode!.Value;

    // -------------------------------------------------------------------------
    // Status code mapping
    // -------------------------------------------------------------------------

    [Fact]
    public void HandleErrors_Returns400_ForFailure()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.Failure("code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void HandleErrors_Returns401_ForUnauthorized()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.Unauthorized("code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void HandleErrors_Returns403_ForForbidden()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.Forbidden("code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void HandleErrors_Returns404_ForNotFound()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.NotFound("code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void HandleErrors_Returns409_ForConflict()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.Conflict("code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void HandleErrors_Returns500_ForUnknownErrorType()
    {
        var controller = CreateController();
        // Error.Custom() creates an error with ErrorType outside the known enum values.
        var result = controller.InvokeHandleErrors([Error.Custom(99, "code", "desc")]);
        StatusOf(result).Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void HandleErrors_Returns500_WhenErrorListIsEmpty()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([]);
        StatusOf(result).Should().Be(StatusCodes.Status500InternalServerError);
    }

    // -------------------------------------------------------------------------
    // Validation error aggregation
    // -------------------------------------------------------------------------

    [Fact]
    public void HandleErrors_Returns422_ForValidationError()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.Validation("Field", "Required")]);
        StatusOf(result).Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void HandleErrors_Returns422_AggregatesMultipleValidationErrors()
    {
        var controller = CreateController();
        var errors = new List<Error>
        {
            Error.Validation("Name",  "Too short"),
            Error.Validation("Email", "Invalid format"),
        };

        var result = controller.InvokeHandleErrors(errors);

        StatusOf(result).Should().Be(StatusCodes.Status422UnprocessableEntity);
        var details = ((ObjectResult)result).Value as ValidationProblemDetails;
        details!.Errors.Should().ContainKey("Name");
        details.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public void HandleErrors_UsesFirstError_WhenMixedTypes()
    {
        // If not all errors are Validation, the first error's type determines the code.
        var controller = CreateController();
        var errors = new List<Error>
        {
            Error.NotFound("first", "First error"),
            Error.Conflict("second", "Second error"),
        };

        var result = controller.InvokeHandleErrors(errors);

        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // Problem Details content
    // -------------------------------------------------------------------------

    [Fact]
    public void HandleErrors_IncludesErrorCode_AsTitle()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.NotFound("User.NotFound", "User not found.")]);

        var details = ((ObjectResult)result).Value as ProblemDetails;
        details!.Title.Should().Be("User.NotFound");
    }

    [Fact]
    public void HandleErrors_IncludesErrorDescription_AsDetail()
    {
        var controller = CreateController();
        var result = controller.InvokeHandleErrors([Error.NotFound("User.NotFound", "User not found.")]);

        var details = ((ObjectResult)result).Value as ProblemDetails;
        details!.Detail.Should().Be("User not found.");
    }
}
