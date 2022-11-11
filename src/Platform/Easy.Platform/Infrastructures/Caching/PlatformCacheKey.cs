using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;

namespace Easy.Platform.Infrastructures.Caching;

/// <summary>
/// Represent the structured cache key. The string formatted value is "{Context}.{Collection}.{RequestKey}";
/// </summary>
public class PlatformCacheKey : IEqualityComparer<PlatformCacheKey>
{
    public const string DefaultContext = "UnknowContext";
    public const string DefaultCollection = "All";
    public const string DefaultRequestKey = "All";
    public const string RequestKeySeparator = ".";
    public const string RequestKeySeparatorAutoValidReplaced = "_";
    public const string RequestKeyPartsSeparator = "--";

    public PlatformCacheKey(string requestKey = DefaultRequestKey)
    {
        RequestKey = AutoFixKeyPartValue(requestKey);
    }

    public PlatformCacheKey(object[] requestKeyParts)
    {
        RequestKey = requestKeyParts.Length == 0 ? DefaultRequestKey : BuildRequestKey(requestKeyParts);
    }

    public PlatformCacheKey(string collection, string requestKey) : this(requestKey)
    {
        Collection = AutoFixKeyPartValue(collection);
    }

    public PlatformCacheKey(string collection, params object[] requestKeyParts) : this(requestKeyParts)
    {
        Collection = AutoFixKeyPartValue(collection);
    }

    public PlatformCacheKey(string context, string collection, string requestKey) : this(collection, requestKey)
    {
        Context = AutoFixKeyPartValue(context);
    }

    public PlatformCacheKey(string context, string collection, params object[] requestKeyParts) : this(
        collection,
        requestKeyParts)
    {
        Context = AutoFixKeyPartValue(context);
    }

    /// <summary>
    /// The context of the cached data. Usually it's like the database or service name.
    /// </summary>
    public string Context { get; init; } = DefaultContext;

    /// <summary>
    /// The Type of the cached data. Usually it's like the database collection or data class name.
    /// </summary>
    public string Collection { get; init; } = DefaultCollection;

    /// <summary>
    /// The request key for cached data. Usually it could be data identifier, or request unique key.
    /// </summary>
    public string RequestKey { get; init; }

    public bool Equals(PlatformCacheKey x, PlatformCacheKey y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;
        if (x.GetType() != y.GetType())
            return false;
        return x.ToString() == y.ToString();
    }

    public int GetHashCode(PlatformCacheKey obj)
    {
        return HashCode.Combine(obj.Context, obj.Collection, obj.RequestKey);
    }

    public static string AutoFixKeyPartValue(string keyPartValue)
    {
        return keyPartValue?.Replace(RequestKeySeparator, RequestKeySeparatorAutoValidReplaced);
    }

    public static implicit operator string(PlatformCacheKey platformCacheKey)
    {
        return platformCacheKey.ToString();
    }

    public static implicit operator PlatformCacheKey(string fullCacheKeyString)
    {
        return FromFullCacheKeyString(fullCacheKeyString);
    }

    public static PlatformCacheKey FromFullCacheKeyString(string fullCacheKeyString)
    {
        var cacheKeyParts = fullCacheKeyString.Split(RequestKeySeparator).ToList();
        return new PlatformCacheKey(cacheKeyParts[0], cacheKeyParts[1], cacheKeyParts[2]);
    }

    public static string BuildRequestKey(object[] requestKeyParts)
    {
        if (requestKeyParts.Length == 0)
            throw new ArgumentException("requestKeyParts must be not empty.", nameof(requestKeyParts));

        return
            $"[{requestKeyParts.Select(p => p ?? "null").Select(p => p.AsJson().Replace("\"", "'")).JoinToString(RequestKeyPartsSeparator)}]";
    }

    public static object[] BuildRequestKeyParts(string requestKey)
    {
        return requestKey
            .Substring(1, requestKey.Length - 1)
            .Split(RequestKeySeparator)
            .Select(
                requestKeyPartJsonString =>
                {
                    try
                    {
                        return PlatformJsonSerializer.Deserialize(requestKeyPartJsonString, typeof(object));
                    }
                    catch (Exception)
                    {
                        return requestKeyPartJsonString;
                    }
                })
            .ToArray();
    }

    public override string ToString()
    {
        return $"{Context}{RequestKeySeparator}{Collection}{RequestKeySeparator}{RequestKey}";
    }

    public object[] RequestKeyParts()
    {
        return BuildRequestKeyParts(RequestKey);
    }
}
