using Easy.Platform.Application;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public override string[] TracingSources()
    {
        return Util.ListBuilder.NewArray(
            IPlatformCacheRepository.ActivitySource.Name);
    }

    protected override IPlatformDistributedCacheRepository DistributedCacheRepositoryProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        return new PlatformRedisDistributedCacheRepository(
            serviceProvider,
            serviceProvider.GetRequiredService<IOptions<RedisCacheOptions>>(),
            serviceProvider.GetRequiredService<IPlatformApplicationSettingContext>(),
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            serviceProvider.GetRequiredService<PlatformCacheSettings>());
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
