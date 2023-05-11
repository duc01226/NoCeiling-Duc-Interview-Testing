using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Common;
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
    public static IPlatformApplicationUserContextAccessor UserContextAccessor => RootServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>();

    public static IPlatformApplicationUserContext CurrentUserContext => UserContextAccessor.Current;
}
