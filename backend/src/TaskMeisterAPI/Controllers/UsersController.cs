using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskMeisterAPI.Infrastructure.Auth;
using TaskMeisterAPI.Infrastructure.ModelBinding;
using TaskMeisterAPI.Models.Entities;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Services;

namespace TaskMeisterAPI.Controllers;

[Route("api/[controller]")]
public class UsersController(IUserService userService) : ApiController
{
    private readonly IUserService _userService = userService;

    /// <summary>Register a new user and receive a JWT.</summary>
    [HttpPost("signup")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        var result = await _userService.SignupAsync(request);
        return result.Match(
            response => StatusCode(StatusCodes.Status201Created, response),
            HandleErrors);
    }

    /// <summary>Authenticate an existing user and receive a JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request);
        return result.Match(Ok, HandleErrors);
    }

    /// <summary>
    /// Invalidate the current JWT by incrementing the user's token version.
    /// All tokens issued before this call are immediately rejected.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ServiceFilter(typeof(RequireUserFilter))] 
    public async Task<IActionResult> Logout([FromUser] User currentUser)
    {
        var result = await _userService.LogoutAsync(currentUser);
        return result.Match(_ => NoContent(), HandleErrors);
    }
}
