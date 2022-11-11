using Easy.Platform.Application.Context;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Easy.Platform.RedisCache;

/// <summary>
/// Add this module to use RedisCache as a PlatformDistributedCache via <see cref="PlatformCacheRepositoryType.Distributed" />
/// </summary>
public abstract class PlatformRedisCacheModule : PlatformCachingModule
{
    public PlatformRedisCacheModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    protected override IPlatformDistributedCacheRepository DistributedCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        return new PlatformRedisDistributedCacheRepository(
            serviceProvider,
            serviceProvider.GetService<IOptions<RedisCacheOptions>>(),
            serviceProvider.GetService<IPlatformApplicationSettingContext>());
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.AddOptions();
        serviceCollection.Configure<RedisCacheOptions>(InternalRegisterSetupRedisCacheOptions);
    }

    protected abstract void SetupRedisCacheOptions(RedisCacheOptions options);

    private void InternalRegisterSetupRedisCacheOptions(RedisCacheOptions options)
    {
        SetupRedisCacheOptions(options);
    }
}
