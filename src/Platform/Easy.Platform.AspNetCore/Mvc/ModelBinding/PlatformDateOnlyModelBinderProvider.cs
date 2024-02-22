using Easy.Platform.Common.JsonSerialization.Converters.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Easy.Platform.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Provides a model binder for DateOnly and DateOnly? types in the ASP.NET Core MVC framework.
/// </summary>
public class PlatformDateOnlyModelBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(DateOnly) ||
            context.Metadata.ModelType == typeof(DateOnly?))
            return new PlatformDateOnlyModelBinder(context.Metadata.ModelType == typeof(DateOnly?));

        return null;
    }
}

/// <summary>
/// Model binder for binding DateOnly and DateOnly? types from request values in the ASP.NET Core MVC framework.
/// </summary>
public class PlatformDateOnlyModelBinder : IModelBinder
{
    private readonly bool isNullable;

    public PlatformDateOnlyModelBinder(bool isNullable)
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

            var parsedDate = PlatformStringToDateTimeConverterHelper.TryReadDateOnly(dateStr)
                .Ensure(v => isNullable || v != null, "Parse DateOnly failed");

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
