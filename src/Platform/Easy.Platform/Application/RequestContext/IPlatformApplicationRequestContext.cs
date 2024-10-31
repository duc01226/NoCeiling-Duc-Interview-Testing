using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;

namespace Easy.Platform.Application.RequestContext;

/// <summary>
/// This is the current context data in the current local thread from IPlatformApplicationRequestContextAccessor. Please never save this context as a property in any Service/Command/Handler.
/// Should use the IPlatformApplicationRequestContextAccessor.Current to get the data.
/// </summary>
/// <remarks>
/// The IPlatformApplicationRequestContext interface represents the current context data in the current local thread. It's used to store and retrieve context-specific data as key-value pairs, where the keys are strings and the values are objects. This context is typically accessed via IPlatformApplicationRequestContextAccessor.Current.
/// <br />
/// This interface is crucial for scenarios where you need to access context-specific data that has been previously stored in the IPlatformApplicationRequestContext. For example, this could be user-specific data, request-specific data, or any other data that needs to be accessed across different parts of the application during the lifetime of a single request or operation.
/// <br />
/// The IPlatformApplicationRequestContext interface includes methods for getting and setting values by key, getting all keys, getting all key-value pairs, and clearing the context. It's implemented by classes like PlatformDefaultApplicationRequestContext and PlatformAspNetApplicationRequestContext, which provide specific implementations for different platforms or scenarios.
/// </remarks>
public interface IPlatformApplicationRequestContext : IDictionary<string, object>
{
    public static HashSet<string> DefaultIgnoreRequestContextKeys { get; } =
    [
        DefaultCommonIgnoredRequestContextKeys.Accept,
        DefaultCommonIgnoredRequestContextKeys.AcceptEncoding,
        DefaultCommonIgnoredRequestContextKeys.AcceptLanguage,
        DefaultCommonIgnoredRequestContextKeys.AuthTime,
        DefaultCommonIgnoredRequestContextKeys.Authorization,
        DefaultCommonIgnoredRequestContextKeys.ContentLength,
        DefaultCommonIgnoredRequestContextKeys.ContentType,
        DefaultCommonIgnoredRequestContextKeys.CorrelationId,
        DefaultCommonIgnoredRequestContextKeys.Exp,
        DefaultCommonIgnoredRequestContextKeys.Iat,
        DefaultCommonIgnoredRequestContextKeys.Priority,
        DefaultCommonIgnoredRequestContextKeys.SecChUa,
        DefaultCommonIgnoredRequestContextKeys.SecChUaMobile,
        DefaultCommonIgnoredRequestContextKeys.SecChUaPlatform,
        DefaultCommonIgnoredRequestContextKeys.SecFetchDest,
        DefaultCommonIgnoredRequestContextKeys.SecFetchMode,
        DefaultCommonIgnoredRequestContextKeys.SecFetchSite,
        DefaultCommonIgnoredRequestContextKeys.SessionId,
        DefaultCommonIgnoredRequestContextKeys.Sid,
        DefaultCommonIgnoredRequestContextKeys.UserAgent,
        DefaultCommonIgnoredRequestContextKeys.Nbf,
        DefaultCommonIgnoredRequestContextKeys.Jti,
        DefaultCommonIgnoredRequestContextKeys.Aud,
        DefaultCommonIgnoredRequestContextKeys.Iss,
        DefaultCommonIgnoredRequestContextKeys.Referer
    ];

    /// <summary>
    /// Retrieves the value associated with the specified context key.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="contextKey">The key of the value to retrieve.</param>
    /// <returns>The value associated with the specified key, if it exists; otherwise, the default value for type T.</returns>
    /// <remarks>
    /// The GetValue[T](string contextKey) method is part of the IPlatformApplicationRequestContext interface, which represents the current context data in the current local thread. This interface is used to store and retrieve context-specific data as key-value pairs, where the keys are strings and the values are objects.
    /// <br />
    /// The GetValue[T](string contextKey) method is specifically used to retrieve the value associated with a specified context key.The method takes a context key as a parameter and returns the value associated with this key, if it exists. If the key does not exist in the context, the method returns the default value for the specified type T.
    /// <br />
    /// This method is useful in scenarios where you need to access context-specific data that has been previously stored in the IPlatformApplicationRequestContext. For example, this could be user-specific data, request-specific data, or any other data that needs to be accessed across different parts of the application during the lifetime of a single request or operation.
    /// </remarks>
    T GetValue<T>(string contextKey);

    /// <summary>
    /// Retrieves the value associated with the specified context key.
    /// </summary>
    /// <param name="valueType">The type of the value to retrieve.</param>
    /// <param name="contextKey">The key of the value to retrieve.</param>
    /// <returns>The value associated with the specified context key. If the specified key is not found, a default value for the type parameter is returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the contextKey is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the contextKey does not exist in the context.</exception>
    /// <remarks>
    /// The GetValue method in the IPlatformApplicationRequestContext interface is used to retrieve a value of a specific type from the application user context. The application user context is a dictionary-like structure that stores key-value pairs, where the key is a string and the value can be any object.
    /// <br />
    /// The GetValue method takes two parameters: valueType and contextKey. The valueType parameter specifies the type of the value to retrieve, and the contextKey parameter is the key associated with the value in the context.
    /// <br />
    /// If the specified contextKey is found in the context, the method returns the associated value. If the contextKey is not found, the method throws a KeyNotFoundException. If the contextKey is null, the method throws an ArgumentNullException.
    /// <br />
    /// This method is implemented in different classes such as PlatformDefaultApplicationRequestContext and PlatformAspNetApplicationRequestContext, which means the actual behavior of the method can vary depending on the specific implementation.
    /// <br />
    /// In general, this method is useful for retrieving user-specific data stored in the application context, which can be used for various purposes such as personalization, session management, and more.
    /// </remarks>
    object GetValue(Type valueType, string contextKey);

    void SetValue(object value, string contextKey);

    /// <summary>
    /// Get all keys of current request context except
    /// </summary>
    List<string> GetAllIncludeIgnoredKeys();

    /// <summary>
    /// Get all key-value pair dict of current request context
    /// </summary>
    IDictionary<string, object?> GetAllIncludeIgnoredKeyValues();

    /// <summary>
    /// Get all keys of current request context except from <see cref="IPlatformApplicationSettingContext.GetIgnoreRequestContextKeys" />
    /// </summary>
    List<string> GetAllKeys();

    /// <summary>
    /// Get all key-value pair dict of current request context except from <see cref="IPlatformApplicationSettingContext.GetIgnoreRequestContextKeys" />
    /// </summary>
    IDictionary<string, object?> GetAllKeyValues();

    /// <summary>
    /// Contains constant field names for keys in HTTP and JWT contexts that can be ignored.
    /// </summary>
    public static class DefaultCommonIgnoredRequestContextKeys
    {
        /// <summary>
        /// Contains the credentials or token used for authorization, typically in the format "Bearer {token}". Part of the HTTP context.
        /// </summary>
        public const string Authorization = "Authorization";

        /// <summary>
        /// Represents a priority level, often used to indicate request priority.
        /// </summary>
        public const string Priority = "priority";

        /// <summary>
        /// The length of the request body, in bytes. Part of the HTTP context.
        /// </summary>
        public const string ContentLength = "Content-Length";

        /// <summary>
        /// Indicates if the user agent is on a mobile device. Part of the HTTP context.
        /// </summary>
        public const string SecChUaMobile = "sec-ch-ua-mobile";

        /// <summary>
        /// Specifies accepted content-encoding methods for the response, such as gzip or deflate. Part of the HTTP context.
        /// </summary>
        public const string AcceptEncoding = "Accept-Encoding";

        /// <summary>
        /// Unique session identifier for the user session. Part of the HTTP context.
        /// </summary>
        public const string SessionId = "session_id";

        /// <summary>
        /// Specifies accepted content types for the response, such as application/json. Part of the HTTP context.
        /// </summary>
        public const string Accept = "Accept";

        /// <summary>
        /// Session ID used to track user sessions. Part of the HTTP context.
        /// </summary>
        public const string Sid = "sid";

        /// <summary>
        /// Indicates the preferred language of the client. Part of the HTTP context.
        /// </summary>
        public const string AcceptLanguage = "Accept-Language";

        /// <summary>
        /// Specifies the user agent (browser and version) making the request. Part of the HTTP context.
        /// </summary>
        public const string UserAgent = "User-Agent";

        /// <summary>
        /// Specifies the platform (e.g., Windows, MacOS) of the user agent. Part of the HTTP context.
        /// </summary>
        public const string SecChUaPlatform = "sec-ch-ua-platform";

        /// <summary>
        /// Indicates the relationship between the current request URL and the referrer. Part of the HTTP context.
        /// </summary>
        public const string SecFetchSite = "sec-fetch-site";

        /// <summary>
        /// Unique identifier for tracking requests across distributed systems. Part of the HTTP context.
        /// </summary>
        public const string CorrelationId = "Correlation-Id";

        /// <summary>
        /// Specifies accepted brands of user agents, often for content optimization. Part of the HTTP context.
        /// </summary>
        public const string SecChUa = "sec-ch-ua";

        /// <summary>
        /// Specifies the destination of the fetch request, e.g., document, image, or empty. Part of the HTTP context.
        /// </summary>
        public const string SecFetchDest = "sec-fetch-dest";

        /// <summary>
        /// The media type of the request body. Part of the HTTP context.
        /// </summary>
        public const string ContentType = "Content-Type";

        /// <summary>
        /// The authentication time of the JWT token, representing when the token was issued.
        /// </summary>
        public const string AuthTime = "auth_time";

        /// <summary>
        /// The expiration time of the JWT token, representing when the token will expire.
        /// </summary>
        public const string Exp = "exp";

        /// <summary>
        /// The issued-at time of the JWT token, representing when the token was created.
        /// </summary>
        public const string Iat = "iat";

        /// <summary>
        /// Specifies the fetch request mode, e.g., cors, no-cors, same-origin. Part of the HTTP context.
        /// </summary>
        public const string SecFetchMode = "sec-fetch-mode";

        /// <summary>
        /// The Not Before time of the JWT token, representing the earliest time at which the token is valid.
        /// </summary>
        public const string Nbf = "nbf";

        /// <summary>
        /// A unique identifier for the JWT token, often used to prevent replay attacks. Part of the JWT context.
        /// </summary>
        public const string Jti = "jti";

        /// <summary>
        /// The audience(s) that the JWT token is intended for, typically identifying the recipient(s) that the token is meant for.
        /// </summary>
        public const string Aud = "aud";

        /// <summary>
        /// The issuer of the JWT token, identifying the principal that issued the token.
        /// </summary>
        public const string Iss = "iss";

        /// <summary>
        /// The address of the previous web page from which a request was made. Part of the HTTP context.
        /// </summary>
        public const string Referer = "Referer";
    }
}

public static class PlatformApplicationRequestContextExtensions
{
    public static IPlatformApplicationRequestContext SetValues(this IPlatformApplicationRequestContext context, IDictionary<string, object> values)
    {
        values.ForEach(p => context.SetValue(p.Value, p.Key));

        return context;
    }

    public static T GetRequestContextValue<T>(this IDictionary<string, object> context, string contextKey)
    {
        if (context is IPlatformApplicationRequestContext userContext)
            return userContext.GetValue<T>(contextKey);
        if (PlatformRequestContextHelper.TryGetValue(context, contextKey, out T item))
            return item;

        return default;
    }

    public static void SetRequestContextValue(this IDictionary<string, object> context, object value, string contextKey)
    {
        if (context is IPlatformApplicationRequestContext userContext)
            userContext.SetValue(value, contextKey);
        else
            context.Upsert(contextKey, value);
    }
}
