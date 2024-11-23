#nullable enable
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using Easy.Platform.Application;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Context.RequestContext.RequestContextKeyToClaimTypeMapper.Abstract;
using Easy.Platform.Common.RequestContext;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Context.RequestContext;

public class PlatformAspNetApplicationRequestContext : IPlatformApplicationRequestContext
{
    private static readonly MethodInfo GetValueByGenericTypeMethodInfo =
        typeof(PlatformAspNetApplicationRequestContext).GetMethods()
            .First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);

    protected readonly IPlatformApplicationSettingContext ApplicationSettingContext;
    protected readonly ConcurrentDictionary<string, object?> IgnoreRequestContextKeysRequestContextData = new();

    private readonly IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly Lock initCachedRequestContextDataLock = new();
    private bool cachedRequestContextDataInitiated;

    public PlatformAspNetApplicationRequestContext(
        IHttpContextAccessor httpContextAccessor,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper,
        IPlatformApplicationSettingContext applicationSettingContext)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.claimTypeMapper = claimTypeMapper;
        ApplicationSettingContext = applicationSettingContext;
    }

    public ConcurrentDictionary<string, object?> FullCachedRequestContextData { get; } = new();

    public T? GetValue<T>(string contextKey)
    {
        return GetValue<T>(
            contextKey,
            CurrentHttpContext(),
            ApplicationSettingContext,
            FullCachedRequestContextData,
            IgnoreRequestContextKeysRequestContextData,
            out _,
            claimTypeMapper);
    }

    public void SetValue(object? value, string contextKey)
    {
        FullCachedRequestContextData.Upsert(contextKey, value);
        if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().ContainsIgnoreCase(contextKey))
            IgnoreRequestContextKeysRequestContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllIncludeIgnoredKeys()
    {
        InitAllKeyValuesForCachedRequestContextData();

        return GetAllKeys(CurrentHttpContext(), true);
    }

    public IDictionary<string, object?> GetAllIncludeIgnoredKeyValues()
    {
        InitAllKeyValuesForCachedRequestContextData();

        return GetAllKeyValues(CurrentHttpContext(), true);
    }

    public List<string> GetAllKeys()
    {
        InitAllKeyValuesForCachedRequestContextData();

        return GetAllKeys(CurrentHttpContext());
    }

    public IDictionary<string, object?> GetAllKeyValues()
    {
        InitAllKeyValuesForCachedRequestContextData();

        return GetAllKeyValues(CurrentHttpContext());
    }

    public void Add(KeyValuePair<string, object> item)
    {
        SetValue(item.Value, item.Key);
    }

    public void Clear()
    {
        FullCachedRequestContextData.Clear();
        IgnoreRequestContextKeysRequestContextData.Clear();
    }

    public bool Contains(KeyValuePair<string, object?> item)
    {
        InitAllKeyValuesForCachedRequestContextData();
        // ReSharper disable once UsageOfDefaultStructEquality
        return FullCachedRequestContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        InitAllKeyValuesForCachedRequestContextData();
        FullCachedRequestContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        IgnoreRequestContextKeysRequestContextData.Remove(item.Key, out _);
        return FullCachedRequestContextData.Remove(item.Key, out _);
    }

    public int Count => FullCachedRequestContextData.Count;
    public bool IsReadOnly => false;

    public object? GetValue(Type valueType, string contextKey)
    {
        return GetValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: [contextKey]);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        InitAllKeyValuesForCachedRequestContextData();
        return FullCachedRequestContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        InitAllKeyValuesForCachedRequestContextData();
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        FullCachedRequestContextData.Upsert(key, value);
        if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().Contains(key))
            IgnoreRequestContextKeysRequestContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        InitAllKeyValuesForCachedRequestContextData();
        return FullCachedRequestContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        InitAllKeyValuesForCachedRequestContextData();

        IgnoreRequestContextKeysRequestContextData.Remove(key, out _);
        return FullCachedRequestContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object? value)
    {
        value = GetValue<object?>(
            key,
            CurrentHttpContext(),
            ApplicationSettingContext,
            FullCachedRequestContextData,
            IgnoreRequestContextKeysRequestContextData,
            out var hasFoundValue,
            claimTypeMapper);
        return hasFoundValue;
    }

    public object? this[string key]
    {
        get => GetValue<object>(key);
        set => SetValue(value, key);
    }

    public ICollection<string> Keys
    {
        get
        {
            InitAllKeyValuesForCachedRequestContextData();
            return FullCachedRequestContextData.Keys;
        }
    }

    public ICollection<object?> Values
    {
        get
        {
            InitAllKeyValuesForCachedRequestContextData();
            return FullCachedRequestContextData.Values;
        }
    }

    /// <summary>
    /// Retrieves the value associated with the specified context key.
    /// </summary>
    /// <param name="contextKey">The key of the value to get.</param>
    /// <param name="useHttpContext">The HttpContext instance to use.</param>
    /// <param name="cachedRequestContextData">The ConcurrentDictionary instance that contains cached user context data.</param>
    /// <param name="hasFoundValue">hasFoundValue</param>
    /// <param name="claimTypeMapper">The IPlatformApplicationRequestContextKeyToClaimTypeMapper instance that maps user context keys to claim types.</param>
    /// <returns>The value associated with the specified context key. If the specified key is not found, a default value is returned.</returns>
    /// <typeparam name="T">The type of the value to get.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown when the contextKey is null.</exception>
    /// <remarks>
    /// The GetValue[T] method in the PlatformAspNetApplicationRequestContext class is used to retrieve a value associated with a specified context key from the user's context data. This method is generic, meaning it can return values of any type.
    /// <br />
    /// The method first checks if the context data is cached in a ConcurrentDictionary instance.If the data is cached, it retrieves the value from the cache.If the data is not cached, it attempts to retrieve the value from the HttpContext instance.If the value is successfully retrieved from the HttpContext, it is then added to the cache for future use.
    /// <br />
    /// This method is useful for efficiently accessing user-specific data that may be needed across multiple requests in an ASP.NET Core application.By caching the data, the method avoids the overhead of repeatedly retrieving the same data from the HttpContext.
    /// <br />
    /// The IPlatformApplicationRequestContextKeyToClaimTypeMapper instance is used to map user context keys to claim types, which can be useful when working with claims-based identity.
    /// </remarks>
    public static T? GetValue<T>(
        string contextKey,
        HttpContext? useHttpContext,
        IPlatformApplicationSettingContext applicationSettingContext,
        ConcurrentDictionary<string, object?> cachedRequestContextData,
        ConcurrentDictionary<string, object?> ignoreRequestContextKeysRequestContextData,
        out bool hasFoundValue,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper? claimTypeMapper = null)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        hasFoundValue = PlatformRequestContextHelper.TryGetValue(cachedRequestContextData, contextKey, out T item);
        if (hasFoundValue) return item;

        hasFoundValue = TryGetValueFromHttpContext(useHttpContext, contextKey, claimTypeMapper, out T? foundValue);
        if (hasFoundValue)
        {
            cachedRequestContextData.TryAdd(contextKey, foundValue);
            if (!applicationSettingContext.GetIgnoreRequestContextKeys().ContainsIgnoreCase(contextKey))
                ignoreRequestContextKeysRequestContextData.TryAdd(contextKey, foundValue);

            return foundValue;
        }

        hasFoundValue = false;
        return default;
    }

    protected List<string> GetAllKeys(HttpContext? useHttpContext, bool includeIgnoredKeys = false)
    {
        if (cachedRequestContextDataInitiated)
            return includeIgnoredKeys ? FullCachedRequestContextData.Keys.ToList() : IgnoreRequestContextKeysRequestContextData.Keys.ToList();

        var manuallySetValueItemsDicKeys = FullCachedRequestContextData.Select(p => p.Key);
        var userClaimsTypeKeys = useHttpContext?.User.Claims.Select(p => p.Type) ?? [];
        var requestHeadersKeys = useHttpContext?.Request.Headers.Select(p => p.Key) ?? [];

        return new[] { PlatformApplicationCommonRequestContextKeys.RequestIdContextKey }
            .Concat(manuallySetValueItemsDicKeys)
            .Concat(userClaimsTypeKeys)
            .Concat(requestHeadersKeys)
            .WhereIf(includeIgnoredKeys == false, p => !ApplicationSettingContext.GetIgnoreRequestContextKeys().ContainsIgnoreCase(p))
            .Distinct()
            .ToList();
    }

    protected IDictionary<string, object?> GetAllKeyValues(HttpContext? useHttpContext, bool includeIgnoredKeys = false)
    {
        if (cachedRequestContextDataInitiated)
            return includeIgnoredKeys ? FullCachedRequestContextData : IgnoreRequestContextKeysRequestContextData;

        return GetAllKeys(useHttpContext, includeIgnoredKeys)
            .Select(
                key => new KeyValuePair<string, object?>(
                    key,
                    GetValue<object>(
                        key,
                        useHttpContext,
                        ApplicationSettingContext,
                        FullCachedRequestContextData,
                        IgnoreRequestContextKeysRequestContextData,
                        out _,
                        claimTypeMapper)))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>
    /// GetAllKeyValues also from HttpContext and other source to auto save data into CachedRequestContext
    /// </summary>
    protected void InitAllKeyValuesForCachedRequestContextData()
    {
        if (cachedRequestContextDataInitiated || httpContextAccessor.HttpContext == null) return;

        lock (initCachedRequestContextDataLock)
        {
            if (cachedRequestContextDataInitiated || httpContextAccessor.HttpContext == null) return;

            // GetAllKeyValues already auto cache item in http context into CachedRequestContextData
            GetAllKeyValues(httpContextAccessor.HttpContext, true);
            cachedRequestContextDataInitiated = true;
        }
    }

    /// <summary>
    /// To get the current http context.
    /// This method is very important and explain the reason why we don't store _httpContextAccessor.HttpContext
    /// to a private variable such as private HttpContext _context = _httpContextAccessor.HttpContext.
    /// The important reason is HttpContext property inside HttpContextAccessor is AsyncLocal property. That's why
    /// we need to keep this behavior or we will face the thread issue or accessing DisposedObject.
    /// More details at: https://github.com/aspnet/AspNetCore/blob/master/src/Http/Http/src/HttpContextAccessor.cs#L16.
    /// </summary>
    /// <returns>The current HttpContext with thread safe.</returns>
    public HttpContext? CurrentHttpContext()
    {
        return httpContextAccessor.HttpContext;
    }

    private static bool TryGetValueFromHttpContext<T>(
        HttpContext? useHttpContext,
        string contextKey,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper? claimTypeMapper,
        out T? foundValue)
    {
        if (useHttpContext == null)
        {
            foundValue = default;
            return false;
        }

        if (contextKey == PlatformApplicationCommonRequestContextKeys.RequestIdContextKey)
            return TryGetRequestId(useHttpContext, out foundValue);

        if (TryGetValueFromUserClaims(useHttpContext.User, contextKey, claimTypeMapper, out foundValue))
            return true;

        if (TryGetValueFromRequestHeaders(useHttpContext.Request.Headers, contextKey, claimTypeMapper, out foundValue))
            return true;

        return false;
    }

    private static bool TryGetValueFromRequestHeaders<T>(
        IHeaderDictionary requestHeaders,
        string contextKey,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper? claimTypeMapper,
        out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = GetContextKeyMappedToOneOfClaimTypes(contextKey, claimTypeMapper);

        var stringRequestHeaderValues =
            contextKeyMappedToOneOfClaimTypes
                .Select(
                    contextKeyMappedToJwtClaimType => requestHeaders
                        .Where(p => p.Key == contextKeyMappedToJwtClaimType || p.Key == contextKeyMappedToJwtClaimType.ToLower())
                        .SelectList(p => p.Value.ToString()))
                .FirstOrDefault(p => p.Any()) ??
            [];

        // Try Get Deserialized value from matchedClaimStringValues
        return PlatformRequestContextHelper.TryGetParsedValuesFromStringValues(out foundValue, stringRequestHeaderValues);
    }

    private static HashSet<string> GetContextKeyMappedToOneOfClaimTypes(string contextKey, IPlatformApplicationRequestContextKeyToClaimTypeMapper? claimTypeMapper)
    {
        return claimTypeMapper?.ToOneOfClaimTypes(contextKey) ?? [contextKey];
    }

    private static bool TryGetRequestId<T>(HttpContext httpContext, out T? foundValue)
    {
        if (httpContext.TraceIdentifier.IsNotNullOrEmpty() && typeof(T) == typeof(string))
        {
            foundValue = (T)(object)httpContext.TraceIdentifier;
            return true;
        }

        foundValue = default;
        return false;
    }

    /// <summary>
    /// Return True if found value and out the value of type <see cref="T" />.
    /// Return false if value is not found and out default of type <see cref="T" />.
    /// </summary>
    private static bool TryGetValueFromUserClaims<T>(
        ClaimsPrincipal userClaims,
        string contextKey,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper? claimTypeMapper,
        out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = GetContextKeyMappedToOneOfClaimTypes(contextKey, claimTypeMapper);

        var matchedClaimStringValues = contextKeyMappedToOneOfClaimTypes.Any()
            ? contextKeyMappedToOneOfClaimTypes
                .Select(contextKeyMappedToJwtClaimType => userClaims.FindAll(contextKeyMappedToJwtClaimType).Select(p => p.Value))
                .Aggregate((current, next) => current.Concat(next).ToList())
                .Distinct()
                .ToList()
            : [];

        // Try Get Deserialized value from matchedClaimStringValues
        return PlatformRequestContextHelper.TryGetParsedValuesFromStringValues(out foundValue, matchedClaimStringValues);
    }
}
