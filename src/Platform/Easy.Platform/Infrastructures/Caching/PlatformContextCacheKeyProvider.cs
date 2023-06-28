using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Infrastructures.Caching;

public interface IPlatformContextCacheKeyProvider
{
    /// <summary>
    /// The context of the cached data. Usually it's like the database or service name.
    /// </summary>
    string Context { get; init; }

    PlatformCacheKey GetKey(
        string collection = PlatformContextCacheKeyProvider.DefaultCollection,
        string requestKey = PlatformContextCacheKeyProvider.DefaultRequestKey);

    PlatformCacheKey GetKey(
        string collection = PlatformContextCacheKeyProvider.DefaultCollection,
        string[] requestKeyParts = null);

    Func<PlatformCacheKey, bool> MatchContextKeyPredicate();
}

public class PlatformContextCacheKeyProvider : IPlatformContextCacheKeyProvider
{
    public const string DefaultCollection = PlatformCacheKey.DefaultCollection;
    public const string DefaultRequestKey = PlatformCacheKey.DefaultRequestKey;

    /// <summary>
    /// The context of the cached data. Usually it's like the database or service name.
    /// </summary>
    public virtual string Context { get; init; }

    public virtual PlatformCacheKey GetKey(
        string collection = DefaultCollection,
        string requestKey = DefaultRequestKey)
    {
        EnsureValidProvider();
        return new PlatformCacheKey(Context, collection ?? DefaultCollection, requestKey);
    }

    public virtual PlatformCacheKey GetKey(string collection = DefaultCollection, string[] requestKeyParts = null)
    {
        EnsureValidProvider();
        return new PlatformCacheKey(
            Context,
            collection ?? DefaultCollection,
            requestKeyParts ??
            new[]
            {
                DefaultRequestKey
            });
    }

    public Func<PlatformCacheKey, bool> MatchContextKeyPredicate()
    {
        return p => p.Context == Context;
    }

    protected virtual PlatformContextCacheKeyProvider EnsureValidProvider()
    {
        return this.Ensure(p => p.Context.IsNotNullOrEmpty(), () => new Exception("Context must be not null"));
    }
}

public class PlatformContextCacheKeyProvider<TFixedImplementationProvider> : PlatformContextCacheKeyProvider
    where TFixedImplementationProvider : PlatformContextCacheKeyProvider<TFixedImplementationProvider>
{
    public static PlatformCacheKey CreateKey(
        string collection = DefaultCollection,
        string requestKey = DefaultRequestKey)
    {
        return Activator.CreateInstance<TFixedImplementationProvider>().GetKey(collection, requestKey);
    }

    public static PlatformCacheKey CreateKey(string collection = DefaultCollection, string[] requestKeyParts = null)
    {
        return Activator.CreateInstance<TFixedImplementationProvider>().GetKey(collection, requestKeyParts);
    }
}
