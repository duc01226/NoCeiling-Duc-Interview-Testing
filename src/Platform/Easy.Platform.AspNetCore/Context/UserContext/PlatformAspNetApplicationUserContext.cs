using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;
using Easy.Platform.Common.RequestContext;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Context.UserContext;

public class PlatformAspNetApplicationUserContext : IPlatformApplicationUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IPlatformApplicationUserContextKeyToClaimTypeMapper claimTypeMapper;
    private readonly MethodInfo getValueByGenericTypeMethodInfo;
    private readonly object initCachedUserContextDataLock = new();
    private bool cachedUserContextDataInitiated;

    public PlatformAspNetApplicationUserContext(
        IHttpContextAccessor httpContextAccessor,
        IPlatformApplicationUserContextKeyToClaimTypeMapper claimTypeMapper)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.claimTypeMapper = claimTypeMapper;
        getValueByGenericTypeMethodInfo =
            GetType().GetMethods().First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);

        InitCachedUserContextData();
    }

    public ConcurrentDictionary<string, object> CachedUserContextData { get; } = new();

    public T GetValue<T>(string contextKey)
    {
        return GetValue<T>(contextKey, CurrentHttpContext());
    }

    public void SetValue(object value, string contextKey)
    {
        if (contextKey == null)
            throw new ArgumentNullException(nameof(contextKey));

        CachedUserContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllKeys()
    {
        return GetAllKeys(CurrentHttpContext());
    }

    public Dictionary<string, object> GetAllKeyValues()
    {
        InitCachedUserContextData();

        return GetAllKeyValues(CurrentHttpContext());
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
        InitCachedUserContextData();
        return CachedUserContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        InitCachedUserContextData();
        CachedUserContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        InitCachedUserContextData();
        return CachedUserContextData.Remove(item.Key, out _);
    }

    public int Count => CachedUserContextData.Count;
    public bool IsReadOnly => false;

    public object GetValue(Type valueType, string contextKey)
    {
        return getValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: new object[] { contextKey });
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        InitCachedUserContextData();
        return CachedUserContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        InitCachedUserContextData();
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        InitCachedUserContextData();
        CachedUserContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        InitCachedUserContextData();
        return CachedUserContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        InitCachedUserContextData();
        return CachedUserContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object value)
    {
        InitCachedUserContextData();
        return CachedUserContextData.TryGetValue(key, out value);
    }

    public object this[string key]
    {
        get
        {
            InitCachedUserContextData();
            return CachedUserContextData[key];
        }
        set
        {
            InitCachedUserContextData();
            CachedUserContextData[key] = value;
        }
    }

    public ICollection<string> Keys => CachedUserContextData.Keys;
    public ICollection<object> Values => CachedUserContextData.Values;

    public T GetValue<T>(string contextKey, HttpContext useHttpContext)
    {
        if (contextKey == null)
            throw new ArgumentNullException(nameof(contextKey));

        if (PlatformRequestContextHelper.TryGetValue(CachedUserContextData, contextKey, out T item)) return item;

        if (TryGetValueFromHttpContext(useHttpContext, contextKey, out T foundValue))
        {
            CachedUserContextData.Upsert(contextKey, foundValue);

            return foundValue;
        }

        return default;
    }

    public List<string> GetAllKeys(HttpContext useHttpContext)
    {
        var manuallySetValueItemsDicKeys = CachedUserContextData.Select(p => p.Key);
        var userClaimsTypeKeys = useHttpContext?.User.Claims.Select(p => p.Type) ?? new List<string>();
        var requestHeadersKeys = useHttpContext?.Request.Headers.Select(p => p.Key) ?? new List<string>();

        return Util.ListBuilder.New(PlatformApplicationCommonUserContextKeys.RequestIdContextKey)
            .Concat(manuallySetValueItemsDicKeys)
            .Concat(userClaimsTypeKeys)
            .Concat(requestHeadersKeys)
            .Distinct()
            .ToList();
    }

    public Dictionary<string, object> GetAllKeyValues(HttpContext useHttpContext)
    {
        return GetAllKeys(useHttpContext)
            .Select(key => new KeyValuePair<string, object>(key, GetValue<object>(key, useHttpContext)))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>
    /// GetAllKeyValues also from HttpContext and other source to auto save data into CachedUserContext
    /// </summary>
    protected void InitCachedUserContextData()
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
        InitCachedUserContextData();

        return httpContextAccessor.HttpContext;
    }

    private bool TryGetValueFromHttpContext<T>(HttpContext useHttpContext, string contextKey, out T foundValue)
    {
        if (useHttpContext == null)
        {
            foundValue = default;
            return false;
        }

        if (contextKey == PlatformApplicationCommonUserContextKeys.RequestIdContextKey)
            return TryGetRequestId(useHttpContext, out foundValue);

        if (TryGetValueFromUserClaims(useHttpContext.User, contextKey, out foundValue))
            return true;

        if (TryGetValueFromRequestHeaders(useHttpContext.Request.Headers, contextKey, out foundValue))
            return true;

        return false;
    }

    private bool TryGetValueFromRequestHeaders<T>(
        IHeaderDictionary requestHeaders,
        string contextKey,
        out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = claimTypeMapper.ToOneOfClaimTypes(contextKey);

        var stringRequestHeaderValues =
            contextKeyMappedToOneOfClaimTypes
                .Select(contextKeyMappedToJwtClaimType => requestHeaders.Where(p => p.Key == contextKeyMappedToJwtClaimType).SelectList(p => p.Value.ToString()))
                .FirstOrDefault(p => p.Any()) ??
            new List<string>();

        // Try Get Deserialized value from matchedClaimStringValues
        return PlatformRequestContextHelper.TryGetParsedValuesFromStringValues(out foundValue, stringRequestHeaderValues);
    }

    private bool TryGetRequestId<T>(HttpContext httpContext, out T foundValue)
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
    private bool TryGetValueFromUserClaims<T>(ClaimsPrincipal userClaims, string contextKey, out T foundValue)
    {
        var contextKeyMappedToOneOfClaimTypes = claimTypeMapper.ToOneOfClaimTypes(contextKey);

        var matchedClaimStringValues =
            contextKeyMappedToOneOfClaimTypes
                .Select(
                    contextKeyMappedToJwtClaimType =>
                        userClaims.FindAll(contextKeyMappedToJwtClaimType)
                            .Select(p => p.Value)
                            .ToList())
                .FirstOrDefault(p => p.Any()) ??
            new List<string>();

        // Try Get Deserialized value from matchedClaimStringValues
        return PlatformRequestContextHelper.TryGetParsedValuesFromStringValues(out foundValue, matchedClaimStringValues);
    }
}
