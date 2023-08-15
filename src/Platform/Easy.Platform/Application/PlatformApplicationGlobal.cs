using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.DependencyInjection;

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
public abstract class PlatformApplicationGlobal : PlatformGlobal
{
    public static IPlatformApplicationUserContextAccessor UserContext => ServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>();

    public static IPlatformApplicationUserContext CurrentUserContext => UserContext.Current;

    public static IPlatformCacheRepositoryProvider CacheRepositoryProvider => ServiceProvider.GetRequiredService<IPlatformCacheRepositoryProvider>();
}
