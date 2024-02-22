using Easy.Platform.Common.JsonSerialization.Converters.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Easy.Platform.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Provides a model binder for DateTime and DateTime? types in the ASP.NET Core MVC framework.
/// </summary>
public class PlatformDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(DateTime) ||
            context.Metadata.ModelType == typeof(DateTime?))
            return new PlatformDateTimeModelBinder(context.Metadata.ModelType == typeof(DateTime?));

        return null;
    }
}

/// <summary>
/// Model binder for binding DateTime and DateTime? types from request values in the ASP.NET Core MVC framework.
/// </summary>
public class PlatformDateTimeModelBinder : IModelBinder
{
    private readonly bool isNullable;

    public PlatformDateTimeModelBinder(bool isNullable)
    {
        this.isNullable = isNullable;
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        // Try to fetch the value of the argument by name
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        try
        {
            var dateStr = valueProviderResult.FirstValue;

            var parsedDate = PlatformStringToDateTimeConverterHelper.TryRead(dateStr)
                .Ensure(v => isNullable || v != null, "Parse DateTime failed");

            bindingContext.Result = ModelBindingResult.Success(parsedDate);

            return Task.CompletedTask;
        }
        catch (Exception)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }
    }
}
