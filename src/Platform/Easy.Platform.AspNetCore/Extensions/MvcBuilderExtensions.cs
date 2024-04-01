using System.Text.Json.Serialization;
using Easy.Platform.AspNetCore.Mvc.ModelBinding;
using Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters;
using Easy.Platform.Common.JsonSerialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.AspNetCore.Extensions;

public static class MvcBuilderExtensions
{
    /// <summary>
    /// Support customize multiple Heterogenous Json format in a same application if you have old and new api different json format standards
    /// https://thomaslevesque.com/2022/09/19/using-multiple-json-serialization-settings-in-aspnet-core/
    /// </summary>
    public static IMvcBuilder AddPlatformNamedJsonOptions(
        this IMvcBuilder builder,
        string settingsName,
        Action<JsonOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(settingsName, configure);
        builder.Services.AddSingleton<IConfigureOptions<MvcOptions>>(
            sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<JsonOptions>>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new AddNamedJsonFormatterConfigureMvcOptions(settingsName, options, loggerFactory);
            });
        return builder;
    }

    /// <summary>
    /// Adds platform-specific JSON serialization options to the MVC builder.
    /// </summary>
    /// <param name="builder">The <see cref="IMvcBuilder" /> to configure.</param>
    /// <param name="useJsonStringEnumConverter">
    /// Indicates whether to use the <see cref="JsonStringEnumConverter" /> for enum values.
    /// </param>
    /// <param name="useCamelCaseNaming">
    /// Indicates whether to use camelCase naming for JSON property names.
    /// </param>
    /// <param name="customConverters">
    /// A list of custom <see cref="JsonConverter" /> instances to be added to the serialization options.
    /// </param>
    /// <param name="ignoreJsonConverterTypes">Input list of default platform json converters that you want to be ignored</param>
    /// <returns>The configured <see cref="IMvcBuilder" />.</returns>
    public static IMvcBuilder AddPlatformJsonOptions(
        this IMvcBuilder builder,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null,
        HashSet<Type> ignoreJsonConverterTypes = null)
    {
        return builder.AddJsonOptions(
            options => PlatformJsonSerializer.ConfigOptions(
                options.JsonSerializerOptions,
                useJsonStringEnumConverter,
                useCamelCaseNaming,
                customConverters,
                ignoreJsonConverterTypes));
    }

    /// <summary>
    /// Adds platform-specific model binder providers to the MVC builder.
    /// </summary>
    /// <param name="builder">The MVC builder to which the model binder providers are added.</param>
    /// <returns>The MVC builder with the added model binder providers.</returns>
    /// <remarks>
    /// This method inserts the `PlatformDateTimeModelBinderProvider` and `PlatformDateOnlyModelBinderProvider` at the start of the model binder providers list in the MVC options.
    /// This gives these binders higher priority over the default binders.
    /// </remarks>
    public static IMvcBuilder AddPlatformModelBinderProviders(
        this IMvcBuilder builder)
    {
        return builder.AddMvcOptions(
            opts =>
            {
                // Insert at starts to priority using platform model binder provider
                opts.ModelBinderProviders.Insert(0, new PlatformDateTimeModelBinderProvider());
                opts.ModelBinderProviders.Insert(0, new PlatformDateOnlyModelBinderProvider());
            });
    }
}
