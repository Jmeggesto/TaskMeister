using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskMeisterAPI.Infrastructure.Auth;

public class RequireUserFilter(ICurrentUser currentUser) : IAsyncAuthorizationFilter
{
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = await _currentUser.GetUserAsync();
        if (user != null)
        {
            return;
        }
        // User ID in token but user not found in DB (maybe deleted)
        context.Result = new NotFoundObjectResult("User not found.");
    }
}