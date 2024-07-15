using System.Reflection;
using Easy.Platform.Application;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Constants;
using Easy.Platform.AspNetCore.Context.RequestContext;
using Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper;
using Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper.Abstract;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Easy.Platform.AspNetCore;

/// <summary>
/// Represents a platform module for ASP.NET Core applications. This class is abstract.
/// </summary>
public abstract class PlatformAspNetCoreModule : PlatformModule
{
    public PlatformAspNetCoreModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    /// <summary>
    /// Gets the action that configures additional tracing for the ASP.NET Core platform module.
    /// </summary>
    /// <remarks>
    /// This action is used to add instrumentation for ASP.NET Core and HTTP client operations.
    /// </remarks>
    /// <value>
    /// The action that configures the <see cref="TracerProviderBuilder" /> for additional tracing.
    /// </value>
    public override Action<TracerProviderBuilder> AdditionalTracingConfigure =>
        builder => builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

    /// <summary>
    /// Gets a value indicating whether the module should automatically seed application data on initialization.
    /// </summary>
    protected override bool AutoScanAssemblyRegisterCqrs => true;

    /// <summary>
    /// Default is True. Override this return to False if you need to seed data manually
    /// </summary>
    protected virtual bool AutoSeedApplicationDataOnInit => true;

    /// <summary>
    /// Gets the allowed CORS origins for the module.
    /// </summary>
    /// <param name="configuration">The configuration for the module.</param>
    /// <returns>An array of strings representing the allowed CORS origins.</returns>
    protected abstract string[] GetAllowCorsOrigins(IConfiguration configuration);

    /// <summary>
    /// <inheritdoc cref="PlatformModule.GetServicesRegisterScanAssemblies" />  <br></br>
    /// For PlatformAspNetCoreModule, by default do not support scan parent application module
    /// </summary>
    public override List<Assembly> GetServicesRegisterScanAssemblies()
    {
        return [Assembly];
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        RegisterUserContext(serviceCollection);
        AddDefaultCorsPolicy(serviceCollection);
        serviceCollection.AddHttpClient();
        GetServicesRegisterScanAssemblies().ForEach(assembly => serviceCollection.RegisterHostedServicesFromType(assembly, typeof(PlatformHostingBackgroundService)));
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

    /// <summary>
    /// Executes the seed data for the application module.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task ExecuteDependencyApplicationModuleSeedData()
    {
        await PlatformApplicationModule.ExecuteDependencyApplicationModuleSeedData(
            moduleTypeDependencies: ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);
    }

    /// <summary>
    /// Adds the default CORS policy for the module.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the policy to.</param>
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
                    builder.WithOrigins(GetAllowCorsOrigins(Configuration) ?? [])
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
        return Util.ListBuilder.NewArray(
            PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
            "Content-Disposition");
    }

    /// <summary>
    /// DefaultCorsPolicyPreflightMaxAge for AddDefaultCorsPolicy and UseDefaultCorsPolicy. Default is 1 day.
    /// </summary>
    protected virtual TimeSpan DefaultCorsPolicyPreflightMaxAge()
    {
        return 1.Days();
    }

    /// <summary>
    /// Registers the user context in the provided service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection where the user context will be registered.</param>
    /// <remarks>
    /// This method adds the HttpContextAccessor to the service collection and registers the PlatformAspNetApplicationRequestContextAccessor as a singleton service for the IPlatformApplicationRequestContextAccessor interface.
    /// It also registers the UserContextKeyToClaimTypeMapper in the service collection.
    /// </remarks>
    protected void RegisterUserContext(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.Register(
            typeof(IPlatformApplicationRequestContextAccessor),
            typeof(PlatformAspNetApplicationRequestContextAccessor),
            ServiceLifeTime.Singleton,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        RegisterUserContextKeyToClaimTypeMapper(serviceCollection);
    }

    /// <summary>
    /// This function is used to register implementation for
    /// <see cref="IPlatformApplicationRequestContextKeyToClaimTypeMapper" />
    /// Default implementation is <see cref="PlatformApplicationRequestContextKeyToJwtClaimTypeMapper" />
    /// </summary>
    /// <returns></returns>
    protected virtual Type UserContextKeyToClaimTypeMapperType()
    {
        return typeof(PlatformApplicationRequestContextKeyToJwtClaimTypeMapper);
    }

    /// <summary>
    /// Registers the UserContextKeyToClaimTypeMapper in the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the service to.</param>
    private void RegisterUserContextKeyToClaimTypeMapper(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(
            typeof(IPlatformApplicationRequestContextKeyToClaimTypeMapper),
            UserContextKeyToClaimTypeMapperType(),
            ServiceLifeTime.Singleton);
    }
}

public static class InitPlatformAspNetCoreModuleExtension
{
    /// <summary>
    /// Init module to start running init for all other modules and this module itself
    /// </summary>
    public static void InitPlatformAspNetCoreModule<TModule>(this IApplicationBuilder app)
        where TModule : PlatformAspNetCoreModule
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TModule>().Init(app).WaitResult();
        }
    }
}
