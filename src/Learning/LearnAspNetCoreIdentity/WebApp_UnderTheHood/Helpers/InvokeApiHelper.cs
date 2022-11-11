using System.Net.Http.Headers;
using System.Text.Json;
using WebApp_UnderTheHood.Auth;
using WebApp_UnderTheHood.Authorization.Dtos;
using WebApp_UnderTheHood.Helpers.Abstract;

namespace WebApp_UnderTheHood.Helpers;

/// <summary>
/// Helper help invoke api, auto inject authorization token by giving credential
/// </summary>
public class InvokeApiHelper : Helper
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly HttpContext httpContext;

    public InvokeApiHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        this.httpClientFactory = httpClientFactory;
        httpContext = httpContextAccessor.HttpContext!;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEndPointResponse"></typeparam>
    /// <param name="httpClientName">Logical http client name. Connected to: builder.Services.AddHttpClient(name: "OurWebApi", ...)</param>
    /// <param name="endpointUrl">endpointUrl</param>
    /// <param name="credential">login credential for authUrl to get authToken</param>
    /// <param name="authUrl">authUrl for login credential</param>
    /// <returns></returns>
    public async Task<TEndPointResponse?> InvokeEndpoint<TEndPointResponse>(
        string httpClientName,
        string endpointUrl,
        Credential credential,
        string authUrl = "auth") where TEndPointResponse : class
    {
        var httpClient = httpClientFactory.CreateClient(name: httpClientName);

        await LoadAuthorizationInfoIntoHttpClient(httpClient, credential, authUrl);

        return await httpClient.GetFromJsonAsync<TEndPointResponse>(endpointUrl);
    }

    // Get JwtToken from api and store it in session for reuse until it expired, then get a new JwtToken again
    private async Task LoadAuthorizationInfoIntoHttpClient(
        HttpClient httpClient,
        Credential credential,
        string authUrl)
    {
        var authJwtTokenResponse =
            GetAuthJwtTokenResponseInSession() ??
            await LoadNewAuthJwtTokenResponse(
                httpClient,
                credential,
                authUrl,
                onLoadSuccess: newAuthJwtTokenResponse =>
                {
                    httpContext.Session.SetString("access_token", JsonSerializer.Serialize(newAuthJwtTokenResponse));
                });

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(AppAuthenticationSchemes.JwtBearerScheme, authJwtTokenResponse.AccessToken);
    }

    private JwtTokenResponseDto? GetAuthJwtTokenResponseInSession()
    {
        var jwtTokenResponseJsonStr = httpContext.Session.GetString("access_token");

        var jwtTokenResponse = !string.IsNullOrEmpty(jwtTokenResponseJsonStr)
            ? JsonSerializer.Deserialize<JwtTokenResponseDto>(jwtTokenResponseJsonStr)
            : null;

        return jwtTokenResponse?.IsAccessTokenExpired() == true ? null : jwtTokenResponse;
    }

    private async Task<JwtTokenResponseDto> LoadNewAuthJwtTokenResponse(
        HttpClient httpClient,
        Credential credential,
        string authUrl,
        Action<JwtTokenResponseDto>? onLoadSuccess = null)
    {
        var authResponse = await httpClient.PostAsJsonAsync(authUrl, credential);

        authResponse.EnsureSuccessStatusCode();

        var authJwtTokenResponseJsonStr = await authResponse.Content.ReadAsStringAsync();

        var authJwtTokenResponse = JsonSerializer.Deserialize<JwtTokenResponseDto>(
            authJwtTokenResponseJsonStr,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

        onLoadSuccess?.Invoke(authJwtTokenResponse);

        return authJwtTokenResponse;
    }
}
