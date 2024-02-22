using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters.Formatters;

/// <summary>
/// Support customize multiple Heterogenous Json format in a same application if you have old and new api different json format standards
/// https://thomaslevesque.com/2022/09/19/using-multiple-json-serialization-settings-in-aspnet-core/
/// </summary>
public class NamedSystemTextJsonOutputFormatter : SystemTextJsonOutputFormatter
{
    public NamedSystemTextJsonOutputFormatter(string settingsName, JsonSerializerOptions jsonSerializerOptions) : base(jsonSerializerOptions)
    {
        SettingsName = settingsName;
    }

    public string SettingsName { get; }

    public override bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        if (context.HttpContext.GetJsonSettingsName() != SettingsName)
            return false;

        return base.CanWriteResult(context);
    }
}
