using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters;

/// <summary>
/// Support customize multiple Heterogenous Json format in a same application if you have old and new api different json format standards
/// https://thomaslevesque.com/2022/09/19/using-multiple-json-serialization-settings-in-aspnet-core/
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class NamedJsonSettingsAttribute : Attribute
{
    public NamedJsonSettingsAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public static class HttpContextExtensions
{
    public static string GetJsonSettingsName(this HttpContext context)
    {
        return context.GetEndpoint()
            ?.Metadata
            .GetMetadata<NamedJsonSettingsAttribute>()
            ?.Name;
    }
}
