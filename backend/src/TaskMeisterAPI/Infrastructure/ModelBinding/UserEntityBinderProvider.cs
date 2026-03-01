using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Infrastructure.ModelBinding;

public class UserEntityBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType != typeof(User))
            return null;

        // Ensure parameter binding and check binding source
        if (context.BindingInfo?.BindingSource != BindingSource.Custom)
            return null;

        return new BinderTypeModelBinder(typeof(UserEntityBinder));
    }
}