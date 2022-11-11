using Easy.Platform.Application.Context.UserContext;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application;

/// <summary>
/// Use this for resolve service via root to run in background thread <br />
/// <code>
/// Util.TaskRunner.QueueIntervalAsyncActionInBackground(
///             token => PlatformApplicationGlobal.RootServiceProvider
///                 .ExecuteInjectScopedAsync(
///                     (IPlatformCacheRepositoryProvider cacheRepositoryProvider) =>
///                     {
///                         cacheRepositoryProvider.Get().RemoveCollectionAsync[JobsForPortalDataCollectionCacheKeyProvider](token);
///                     }),
///             intervalTimeInSeconds: 5,
///             logger,
///             maximumIntervalExecutionCount: 3,
///             executeOnceImmediately: true,
///             cancellationToken);
/// </code>
/// </summary>
public static class PlatformApplicationGlobal
{
    public static IServiceProvider RootServiceProvider { get; set; }

    public static IPlatformApplicationUserContextAccessor UserContextAccessor =>
        RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>();

    public static IPlatformApplicationUserContext CurrentUserContext => UserContextAccessor.Current;

    public static ILoggerFactory LoggerFactory =>
        RootServiceProvider.GetRequiredService<ILoggerFactory>();
}
