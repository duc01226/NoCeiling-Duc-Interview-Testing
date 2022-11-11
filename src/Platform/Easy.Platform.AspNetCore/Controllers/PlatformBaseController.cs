using Easy.Platform.Common.Cqrs;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Easy.Platform.AspNetCore.Controllers;

public abstract class PlatformBaseController : ControllerBase
{
    public PlatformBaseController(
        IPlatformCqrs cqrs,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        IConfiguration configuration)
    {
        Cqrs = cqrs;
        CacheRepositoryProvider = cacheRepositoryProvider;
        Configuration = configuration;
    }

    protected IPlatformCqrs Cqrs { get; }
    protected IPlatformCacheRepositoryProvider CacheRepositoryProvider { get; }
    public IConfiguration Configuration { get; }
}
