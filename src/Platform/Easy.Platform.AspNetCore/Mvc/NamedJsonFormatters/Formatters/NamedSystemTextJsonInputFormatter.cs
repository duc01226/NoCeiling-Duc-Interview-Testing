using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters.Formatters;

/// <summary>
/// Support customize multiple Heterogenous Json format in a same application if you have old and new api different json format standards
/// https://thomaslevesque.com/2022/09/19/using-multiple-json-serialization-settings-in-aspnet-core/
/// </summary>
public class NamedSystemTextJsonInputFormatter : SystemTextJsonInputFormatter
{
    public NamedSystemTextJsonInputFormatter(string settingsName, JsonOptions options, ILogger<NamedSystemTextJsonInputFormatter> logger)
        : base(options, logger)
    {
        SettingsName = settingsName;
    }

    public string SettingsName { get; }

    public override bool CanRead(InputFormatterContext context)
    {
        if (context.HttpContext.GetJsonSettingsName() != SettingsName)
            return false;

        return base.CanRead(context);
    }
}
