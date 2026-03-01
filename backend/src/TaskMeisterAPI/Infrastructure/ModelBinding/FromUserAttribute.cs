using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskMeisterAPI.Infrastructure.ModelBinding;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromUserAttribute : Attribute, IModelNameProvider, IBindingSourceMetadata
{
    public string? Name { get; set; }

    public BindingSource BindingSource => BindingSource.Custom;
}