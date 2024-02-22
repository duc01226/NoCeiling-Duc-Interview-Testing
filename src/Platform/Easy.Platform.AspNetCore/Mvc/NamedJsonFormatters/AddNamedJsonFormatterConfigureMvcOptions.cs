using Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters.Formatters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Easy.Platform.AspNetCore.Mvc.NamedJsonFormatters;

/// <summary>
/// Support customize multiple Heterogenous Json format in a same application if you have old and new api different json format standards <br />
/// https://thomaslevesque.com/2022/09/19/using-multiple-json-serialization-settings-in-aspnet-core/ <br />
/// The Configure method of this class will be called every time the options system needs to build a MvcOptions object. <br />
/// </summary>
public class AddNamedJsonFormatterConfigureMvcOptions : IConfigureOptions<MvcOptions>
{
    /// <summary>
    /// We depend on IOptionsMonitor[JsonOptions] to be able to access named options (IOptions[JsonOptions] doesn’t offer this feature)
    /// </summary>
    private readonly IOptionsMonitor<JsonOptions> jsonOptionsMonitor;

    private readonly string jsonSettingsName;
    private readonly ILoggerFactory loggerFactory;

    public AddNamedJsonFormatterConfigureMvcOptions(
        string jsonSettingsName,
        IOptionsMonitor<JsonOptions> jsonOptionsMonitor,
        ILoggerFactory loggerFactory)
    {
        this.jsonSettingsName = jsonSettingsName;
        this.jsonOptionsMonitor = jsonOptionsMonitor;
        this.loggerFactory = loggerFactory;
    }

    public void Configure(MvcOptions options)
    {
        var jsonOptions = jsonOptionsMonitor.Get(jsonSettingsName);
        var logger = loggerFactory.CreateLogger<NamedSystemTextJsonInputFormatter>();

        /*
         * We insert our formatters in the first position, because ASP.NET Core tries each formatter in order.
         * If we added ours at the end, it would pick the first compatible formatter, i.e. the default JSON formatter, ignoring our formatter.
         */
        options.InputFormatters.Insert(
            0,
            new NamedSystemTextJsonInputFormatter(
                jsonSettingsName,
                jsonOptions,
                logger));
        options.OutputFormatters.Insert(
            0,
            new NamedSystemTextJsonOutputFormatter(
                jsonSettingsName,
                jsonOptions.JsonSerializerOptions));
    }
}
