using Easy.Platform.Infrastructures.Caching;

namespace Easy.Platform.Application.Caching;

public abstract class PlatformApplicationCollectionCacheKeyProvider<TFixedImplementationProvider> : PlatformCollectionCacheKeyProvider<TFixedImplementationProvider>
    where TFixedImplementationProvider : PlatformCollectionCacheKeyProvider<TFixedImplementationProvider>
{
    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    public PlatformApplicationCollectionCacheKeyProvider(
        IPlatformApplicationSettingContext applicationSettingContext)
    {
        this.applicationSettingContext = applicationSettingContext;
    }

    public override string Context => applicationSettingContext.ApplicationName;
}
