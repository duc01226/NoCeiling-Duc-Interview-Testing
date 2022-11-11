using Microsoft.Extensions.Configuration;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Provides the cache options for an entry in <see cref="IPlatformCacheRepository"/>.
/// </summary>
public class PlatformCacheEntryOptions
{
    public const int DefaultExpirationInSeconds = 3600;

    /// <summary>
    /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
    /// This will not extend the entry lifetime beyond the absolute expiration (if set).
    /// </summary>
    public virtual double? UnusedExpirationInSeconds { get; set; }

    /// <summary>
    /// Gets or sets an expiration time after seconds, relative to now.
    /// </summary>
    public virtual double? AbsoluteExpirationInSeconds { get; set; } = DefaultExpirationInSeconds;

    /// <summary>
    /// Gets or sets an absolute expiration time, relative to now.
    /// </summary>
    public TimeSpan? AbsoluteExpirationRelativeToNow()
    {
        return AbsoluteExpirationInSeconds.HasValue
            ? TimeSpan.FromSeconds(AbsoluteExpirationInSeconds.Value)
            : null;
    }

    /// <summary>
    /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
    /// This will not extend the entry lifetime beyond the absolute expiration (if set).
    /// </summary>
    public TimeSpan? SlidingExpiration()
    {
        return UnusedExpirationInSeconds.HasValue ? TimeSpan.FromSeconds(UnusedExpirationInSeconds.Value) : null;
    }

    public PlatformCacheEntryOptions WithOptionalCustomAbsoluteExpirationInSeconds(double? value)
    {
        if (value != null)
            AbsoluteExpirationInSeconds = value;
        return this;
    }
}

public abstract class PlatformConfigurationCacheEntryOptions : PlatformCacheEntryOptions
{
    protected readonly IConfiguration Configuration;

    public PlatformConfigurationCacheEntryOptions(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public abstract override double? AbsoluteExpirationInSeconds { get; }

    public abstract override double? UnusedExpirationInSeconds { get; }
}
