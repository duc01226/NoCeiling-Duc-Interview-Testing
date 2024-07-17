using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Context.RequestContext.RequestContextKeyToClaimTypeMapper.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.AspNetCore.Context.RequestContext;

/// <summary>
/// Implementation of <see cref="IPlatformApplicationRequestContextAccessor" />
/// Inspired by Microsoft.AspNetCore.Http.HttpContextAccessor
/// </summary>
public class PlatformAspNetApplicationRequestContextAccessor : PlatformDefaultApplicationRequestContextAccessor
{
    private readonly IServiceProvider serviceProvider;

    public PlatformAspNetApplicationRequestContextAccessor(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    protected override IPlatformApplicationRequestContext CreateNewContext()
    {
        var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        var claimTypeMapper = serviceProvider.GetService<IPlatformApplicationRequestContextKeyToClaimTypeMapper>();

        if (httpContextAccessor == null || claimTypeMapper == null)
            throw new Exception(
                "[Developer] Missing registered IHttpContextAccessor or IPlatformApplicationRequestContextKeyToClaimTypeMapper");

        return new PlatformAspNetApplicationRequestContext(httpContextAccessor, claimTypeMapper);
    }
}
