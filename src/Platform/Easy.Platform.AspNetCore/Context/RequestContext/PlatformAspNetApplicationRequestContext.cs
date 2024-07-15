using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper.Abstract;
using Easy.Platform.Common.RequestContext;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Context.RequestContext;

public class PlatformAspNetApplicationRequestContext : IPlatformApplicationRequestContext
{
    private static readonly MethodInfo GetValueByGenericTypeMethodInfo =
        typeof(PlatformAspNetApplicationRequestContext).GetMethods()
            .First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);

    private readonly IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly object initCachedUserContextDataLock = new();
    private bool cachedUserContextDataInitiated;

    public PlatformAspNetApplicationRequestContext(
        IHttpContextAccessor httpContextAccessor,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.claimTypeMapper = claimTypeMapper;
    }

    public ConcurrentDictionary<string, object> CachedUserContextData { get; } = new();

    public T GetValue<T>(string contextKey)
    {
        return GetValue<T>(contextKey, CurrentHttpContext(), CachedUserContextData, out _, claimTypeMapper);
    }

    public void SetValue(object value, string contextKey)
    {
        CachedUserContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllKeys()
    {
        return GetAllKeys(CurrentHttpContext());
    }

    public Dictionary<string, object> GetAllKeyValues(HashSet<string>? ignoreKeys = null)
    {
        InitAllKeyValuesForCachedUserContextData();

        return GetAllKeyValues(CurrentHttpContext(), ignoreKeys);
    }

    public void Add(KeyValuePair<string, object> item)
    {
        SetValue(item.Value, item.Key);
    }

    public void Clear()
    {
        CurrentHttpContext()?.Items.Clear();
        CachedUserContextData.Clear();
    }

    public bool Contains(KeyValuePair<string, object> item)
    {
        InitAllKeyValuesForCachedUserContextData();
        return CachedUserContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        InitAllKeyValuesForCachedUserContextData();
        CachedUserContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        return CachedUserContextData.Remove(item.Key, out _);
    }

    public int Count => CachedUserContextData.Count;
    public bool IsReadOnly => false;

    public object GetValue(Type valueType, string contextKey)
    {
        return GetValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: [contextKey]);
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        InitAllKeyValuesForCachedUserContextData();
        return CachedUserContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        InitAllKeyValuesForCachedUserContextData();
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        CachedUserContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        InitAllKeyValuesForCachedUserContextData();
        return CachedUserContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        InitAllKeyValuesForCachedUserContextData();
        return CachedUserContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object value)
    {
        value = GetValue<object>(key, CurrentHttpContext(), CachedUserContextData, out var hasFoundValue, claimTypeMapper);
        return hasFoundValue;
    }

    public object this[string key]
    {
        get => GetValue<object>(key);
        set => SetValue(value, key);
    }

    public ICollection<string> Keys
    {
        get
        {
            InitAllKeyValuesForCachedUserContextData();
            return CachedUserContextData.Keys;
        }
    }

    public ICollection<object> Values
    {
        get
        {
            InitAllKeyValuesForCachedUserContextData();
            return CachedUserContextData.Values;
        }
    }

    /// <summary>
    /// Retrieves the value associated with the specified context key.
    /// </summary>
    /// <param name="contextKey">The key of the value to get.</param>
    /// <param name="useHttpContext">The HttpContext instance to use.</param>
    /// <param name="cachedUserContextData">The ConcurrentDictionary instance that contains cached user context data.</param>
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
    public static T GetValue<T>(
        string contextKey,
        HttpContext useHttpContext,
        ConcurrentDictionary<string, object> cachedUserContextData,
        out bool hasFoundValue,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper = null)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        hasFoundValue = PlatformRequestContextHelper.TryGetValue(cachedUserContextData, contextKey, out T item);
        if (hasFoundValue) return item;

        hasFoundValue = TryGetValueFromHttpContext(useHttpContext, contextKey, claimTypeMapper, out T foundValue);
        if (hasFoundValue)
        {
            cachedUserContextData.TryAdd(contextKey, foundValue);

            return foundValue;
        }

        hasFoundValue = false;
        return default;
    }

    protected List<string> GetAllKeys(HttpContext useHttpContext)
    {
        var manuallySetValueItemsDicKeys = CachedUserContextData.Select(p => p.Key);
        var userClaimsTypeKeys = useHttpContext?.User.Claims.Select(p => p.Type) ?? [];
        var requestHeadersKeys = useHttpContext?.Request.Headers.Select(p => p.Key) ?? [];

        return Util.ListBuilder.New(PlatformApplicationCommonRequestContextKeys.RequestIdContextKey)
            .Concat(manuallySetValueItemsDicKeys)
            .Concat(userClaimsTypeKeys)
            .Concat(requestHeadersKeys)
            .Distinct()
            .ToList();
    }

    protected Dictionary<string, object> GetAllKeyValues(HttpContext useHttpContext, HashSet<string>? ignoreKeys = null)
    {
        return GetAllKeys(useHttpContext)
            .WhereIf(ignoreKeys?.Any() == true, key => !ignoreKeys.Contains(key))
            .Select(key => new KeyValuePair<string, object>(key, GetValue<object>(key, useHttpContext, CachedUserContextData, out var _, claimTypeMapper)))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>
    /// GetAllKeyValues also from HttpContext and other source to auto save data into CachedUserContext
    /// </summary>
    protected void InitAllKeyValuesForCachedUserContextData()
    {
        if (cachedUserContextDataInitiated || httpContextAccessor.HttpContext == null) return;

        lock (initCachedUserContextDataLock)
        {
            if (cachedUserContextDataInitiated || httpContextAccessor.HttpContext == null) return;

            // GetAllKeyValues already auto cache item in http context into CachedUserContextData
            GetAllKeyValues(httpContextAccessor.HttpContext);
            cachedUserContextDataInitiated = true;
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
    public HttpContext CurrentHttpContext()
    {
        return httpContextAccessor.HttpContext;
    }

    private static bool TryGetValueFromHttpContext<T>(
        HttpContext useHttpContext,
        string contextKey,
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper,
        out T foundValue)
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
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper,
        out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = GetContextKeyMappedToOneOfClaimTypes<T>(contextKey, claimTypeMapper);

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

    private static HashSet<string> GetContextKeyMappedToOneOfClaimTypes<T>(string contextKey, IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper)
    {
        return claimTypeMapper?.ToOneOfClaimTypes(contextKey) ?? [contextKey];
    }

    private static bool TryGetRequestId<T>(HttpContext httpContext, out T foundValue)
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
        IPlatformApplicationRequestContextKeyToClaimTypeMapper claimTypeMapper,
        out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = GetContextKeyMappedToOneOfClaimTypes<T>(contextKey, claimTypeMapper);

        var matchedClaimStringValues = contextKeyMappedToOneOfClaimTypes
            .Select(contextKeyMappedToJwtClaimType => userClaims.FindAll(contextKeyMappedToJwtClaimType).Select(p => p.Value))
            .Aggregate((current, next) => current.Concat(next).ToList())
            .Distinct()
            .ToList();

        // Try Get Deserialized value from matchedClaimStringValues
        return PlatformRequestContextHelper.TryGetParsedValuesFromStringValues(out foundValue, matchedClaimStringValues);
    }
}
