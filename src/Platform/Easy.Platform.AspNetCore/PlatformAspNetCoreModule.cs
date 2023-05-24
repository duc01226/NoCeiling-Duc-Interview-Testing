using Easy.Platform.Application;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.AspNetCore.Constants;
using Easy.Platform.AspNetCore.Context.UserContext;
using Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper;
using Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Easy.Platform.AspNetCore;

/// <inheritdoc cref="PlatformModule" />
public abstract class PlatformAspNetCoreModule : PlatformModule
{
    public PlatformAspNetCoreModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override Action<TracerProviderBuilder> AdditionalTracingConfigure =>
        builder => builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

    protected override bool AutoScanAssemblyRegisterCqrs => true;

    /// <summary>
    /// Default is True. Override this return to False if you need to seed data manually
    /// </summary>
    protected virtual bool AutoSeedApplicationDataOnInit => true;

    protected abstract string[] GetAllowCorsOrigins(IConfiguration configuration);

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        RegisterUserContext(serviceCollection);
        AddDefaultCorsPolicy(serviceCollection);
        serviceCollection.AddHttpClient();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await IPlatformPersistenceModule.ExecuteDependencyPersistenceModuleMigrateApplicationData(
            moduleTypeDependencies: ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);

        if (IsRootModule && AutoSeedApplicationDataOnInit) await ExecuteDependencyApplicationModuleSeedData();

        LogCommonAspEnvironmentVariableValues();

        void LogCommonAspEnvironmentVariableValues()
        {
            Logger.LogInformation("[PlatformModule] EnvironmentVariable AspCoreEnvironmentValue={AspCoreEnvironmentValue}", PlatformEnvironment.AspCoreEnvironmentValue);
            Logger.LogInformation("[PlatformModule] EnvironmentVariable AspCoreUrlsValue={AspCoreUrlsValue}", PlatformEnvironment.AspCoreUrlsValue);
        }
    }

    public async Task ExecuteDependencyApplicationModuleSeedData()
    {
        await PlatformApplicationModule.ExecuteDependencyApplicationModuleSeedData(
            moduleTypeDependencies: ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);
    }

    protected virtual void AddDefaultCorsPolicy(IServiceCollection serviceCollection)
    {
        serviceCollection.AddCors(
            options => options.AddPolicy(
                PlatformAspNetCoreModuleDefaultPolicies.DevelopmentCorsPolicy,
                builder =>
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithExposedHeaders(DefaultCorsPolicyExposedHeaders())
                        .SetPreflightMaxAge(DefaultCorsPolicyPreflightMaxAge())));

        serviceCollection.AddCors(
            options => options.AddPolicy(
                PlatformAspNetCoreModuleDefaultPolicies.CorsPolicy,
                builder =>
                    builder.WithOrigins(GetAllowCorsOrigins(Configuration) ?? Array.Empty<string>())
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .WithExposedHeaders(DefaultCorsPolicyExposedHeaders())
                        .SetPreflightMaxAge(DefaultCorsPolicyPreflightMaxAge())));
    }

    /// <summary>
    /// Used to override WithExposedHeaders for Cors. Default has
    /// <see cref="PlatformAspnetConstant.CommonHttpHeaderNames.RequestId" />
    /// </summary>
    protected virtual string[] DefaultCorsPolicyExposedHeaders()
    {
        return Util.ListBuilder.NewArray(PlatformAspnetConstant.CommonHttpHeaderNames.RequestId);
    }

    /// <summary>
    /// DefaultCorsPolicyPreflightMaxAge for AddDefaultCorsPolicy and UseDefaultCorsPolicy. Default is 1 day.
    /// </summary>
    protected virtual TimeSpan DefaultCorsPolicyPreflightMaxAge()
    {
        return 1.Days();
    }

    protected void RegisterUserContext(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.Register(
            typeof(IPlatformApplicationUserContextAccessor),
            typeof(PlatformAspNetApplicationUserContextAccessor),
            ServiceLifeTime.Singleton,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        RegisterUserContextKeyToClaimTypeMapper(serviceCollection);
    }

    /// <summary>
    /// This function is used to register implementation for
    /// <see cref="IPlatformApplicationUserContextKeyToClaimTypeMapper" />
    /// Default implementation is <see cref="PlatformApplicationUserContextKeyToJwtClaimTypeMapper" />
    /// </summary>
    /// <returns></returns>
    protected virtual Type UserContextKeyToClaimTypeMapperType()
    {
        return typeof(PlatformApplicationUserContextKeyToJwtClaimTypeMapper);
    }

    private void RegisterUserContextKeyToClaimTypeMapper(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(
            typeof(IPlatformApplicationUserContextKeyToClaimTypeMapper),
            UserContextKeyToClaimTypeMapperType());
    }
}

public static class InitPlatformAspNetCoreModuleExtension
{
    /// <summary>
    /// Init module to start running init for all other modules and this module itself
    /// </summary>
    public static void InitPlatformAspNetCoreModule<TModule>(this WebApplication webApplication)
        where TModule : PlatformAspNetCoreModule
    {
        using (var scope = webApplication.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TModule>().Init().WaitResult();
        }
    }
}
