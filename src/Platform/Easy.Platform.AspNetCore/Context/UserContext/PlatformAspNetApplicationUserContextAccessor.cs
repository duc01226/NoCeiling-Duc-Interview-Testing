using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Context.UserContext.Default;
using Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.AspNetCore.Context.UserContext;

/// <summary>
/// Implementation of <see cref="IPlatformApplicationUserContextAccessor"/>
/// Inspired by Microsoft.AspNetCore.Http.HttpContextAccessor
/// </summary>
public sealed class PlatformAspNetApplicationUserContextAccessor : PlatformDefaultApplicationUserContextAccessor
{
    private readonly IServiceProvider serviceProvider;

    public PlatformAspNetApplicationUserContextAccessor(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    protected override IPlatformApplicationUserContext CreateNewContext()
    {
        var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        var claimTypeMapper = serviceProvider.GetService<IPlatformApplicationUserContextKeyToClaimTypeMapper>();
        if (httpContextAccessor == null || claimTypeMapper == null)
            throw new Exception(
                "[Developer] Missing registered IHttpContextAccessor or IPlatformApplicationUserContextKeyToClaimTypeMapper");
        return new PlatformAspNetApplicationUserContext(httpContextAccessor, claimTypeMapper);
    }
}
