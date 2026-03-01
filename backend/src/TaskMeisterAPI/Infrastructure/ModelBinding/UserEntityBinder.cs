using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskMeisterAPI.Infrastructure.Auth;

namespace TaskMeisterAPI.Infrastructure.ModelBinding;

public class UserEntityBinder : IModelBinder
{
    private readonly ICurrentUser _currentUser;

    public UserEntityBinder(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var user = await _currentUser.GetUserAsync();
        if (user != null)
        {
            bindingContext.Result = ModelBindingResult.Success(user);
        }
        else
        {
            // No user found – binding fails (filter will already have handled this if used)
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}