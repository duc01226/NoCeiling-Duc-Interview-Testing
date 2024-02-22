using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Infrastructures.Caching;

public class PlatformCollectionCacheKeyProvider : PlatformContextCacheKeyProvider
{
    /// <summary>
    /// The Type of the cached data. Usually it's like the database collection or data class name.
    /// </summary>
    public virtual string Collection { get; init; }

    public PlatformCacheKey GetKey(string requestKey = DefaultRequestKey)
    {
        EnsureValidProvider();
        return new PlatformCacheKey(Context, Collection, requestKey ?? DefaultRequestKey);
    }

    public PlatformCacheKey GetKey(string[] requestKeyParts = null)
    {
        EnsureValidProvider();
        return new PlatformCacheKey(
            Context,
            Collection,
            requestKeyParts?.Any() == true
                ? requestKeyParts
                :
                [
                    DefaultRequestKey
                ]);
    }

    public Func<PlatformCacheKey, bool> MatchCollectionKeyPredicate()
    {
        return p => p.Collection == PlatformCacheKey.AutoFixKeyPartValue(Collection) &&
                    p.Context == PlatformCacheKey.AutoFixKeyPartValue(Context);
    }

    protected override PlatformCollectionCacheKeyProvider EnsureValidProvider()
    {
        return this.Ensure(
            p => p.Context.IsNotNullOrEmpty() && p.Collection.IsNotNullOrEmpty(),
            () => new Exception("Context and Collection must be not null"));
    }
}

public abstract class PlatformCollectionCacheKeyProvider<TFixedImplementationProvider> : PlatformCollectionCacheKeyProvider
    where TFixedImplementationProvider : PlatformCollectionCacheKeyProvider<TFixedImplementationProvider>
{
    public static PlatformCacheKey CreateKey(string requestKey = DefaultRequestKey)
    {
        return Activator.CreateInstance<TFixedImplementationProvider>().GetKey(requestKey);
    }

    public static PlatformCacheKey CreateKey(string[] requestKeyParts = null)
    {
        return Activator.CreateInstance<TFixedImplementationProvider>().GetKey(requestKeyParts);
    }
}
