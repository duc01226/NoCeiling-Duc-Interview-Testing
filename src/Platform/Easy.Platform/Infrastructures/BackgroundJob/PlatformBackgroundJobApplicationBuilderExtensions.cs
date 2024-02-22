using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.BackgroundJob;

public static class PlatformBackgroundJobApplicationBuilderExtensions
{
    /// <summary>
    /// Add default support dashboard ui route. Default path is <see cref="PlatformBackgroundJobUseDashboardUiOptions.DashboardUiPathStart" />. <br />
    /// Please UseDashboardUi after init module
    /// </summary>
    public static IApplicationBuilder UseDashboardUi<TBackgroundJobModule>(
        [NotNull] this IApplicationBuilder app,
        PlatformBackgroundJobUseDashboardUiOptions options = null) where TBackgroundJobModule : PlatformBackgroundJobModule
    {
        app.ApplicationServices.GetRequiredService<TBackgroundJobModule>().UseDashboardUi(app, options);

        return app;
    }
}
