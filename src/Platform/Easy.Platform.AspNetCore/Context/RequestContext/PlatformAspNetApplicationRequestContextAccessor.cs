using Easy.Platform.Application;
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
    public PlatformAspNetApplicationRequestContextAccessor(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected override IPlatformApplicationRequestContext CreateNewContext()
    {
        var httpContextAccessor = ServiceProvider.GetService<IHttpContextAccessor>();
        var claimTypeMapper = ServiceProvider.GetService<IPlatformApplicationRequestContextKeyToClaimTypeMapper>();
        var applicationSettingContext = ServiceProvider.GetService<IPlatformApplicationSettingContext>();

        if (httpContextAccessor == null || claimTypeMapper == null)
            throw new Exception(
                "[Developer] Missing registered IHttpContextAccessor or IPlatformApplicationRequestContextKeyToClaimTypeMapper");

        return new PlatformAspNetApplicationRequestContext(httpContextAccessor, claimTypeMapper, applicationSettingContext);
    }
}
