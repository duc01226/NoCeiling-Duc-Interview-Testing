using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.HangfireBackgroundJob;

public abstract class PlatformHangfireBackgroundJobModule : PlatformBackgroundJobModule
{
    public static readonly string DefaultHangfireBackgroundJobAppSettingsName = "HangfireBackgroundJob";

    protected PlatformHangfireBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) :
        base(serviceProvider, configuration)
    {
    }

    protected abstract PlatformHangfireBackgroundJobStorageType UseBackgroundJobStorage();

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.AddHangfire(GlobalConfigurationConfigure);

        serviceCollection.RegisterAllForImplementation<PlatformHangfireBackgroundJobScheduler>(
            ServiceLifeTime.Transient,
            replaceStrategy: DependencyInjectionExtension.ReplaceServiceStrategy.ByService);

        serviceCollection.Register<IPlatformBackgroundJobProcessingService>(
            provider => new PlatformHangfireBackgroundJobProcessingService(
                options: BackgroundJobServerOptionsConfigure(provider, new BackgroundJobServerOptions())),
            ServiceLifeTime.Singleton,
            replaceStrategy: DependencyInjectionExtension.ReplaceServiceStrategy.ByService);
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        // WHY: Config GlobalConfiguration on init module to take advantaged that the persistence module has initiated
        // (convention persistence module should be imported before infrastructure module like background job) so that db is generated.
        GlobalConfigurationConfigure(GlobalConfiguration.Configuration);

        // UseActivator on init so that ServiceProvider have enough all registered services
        GlobalConfiguration.Configuration.UseActivator(new PlatformHangfireActivator(ServiceProvider));

        await ReplaceAllLatestRecurringBackgroundJobs(serviceScope);

        await StartBackgroundJobProcessing(serviceScope);
    }

    // By default We only want one worker run at a time to prevent stacking multiple background job
    // running at the same time could cause performance issues
    protected virtual BackgroundJobServerOptions BackgroundJobServerOptionsConfigure(
        IServiceProvider provider,
        BackgroundJobServerOptions options)
    {
        options.WorkerCount = 1;

        return options;
    }

    protected virtual void GlobalConfigurationConfigure(IGlobalConfiguration configuration)
    {
        configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings();

        switch (UseBackgroundJobStorage())
        {
            case PlatformHangfireBackgroundJobStorageType.InMemory:
            {
                configuration.UseInMemoryStorage();
                break;
            }

            case PlatformHangfireBackgroundJobStorageType.Sql:
            {
                var options = UseSqlServerStorageOptions();
                configuration.UseSqlServerStorage(
                    options.ConnectionString,
                    options.StorageOptions);
                break;
            }

            case PlatformHangfireBackgroundJobStorageType.Mongo:
            {
                var options = UseMongoStorageOptions();
                configuration.UseMongoStorage(
                    options.ConnectionString,
                    options.DatabaseName,
                    options.StorageOptions);
                break;
            }

            case PlatformHangfireBackgroundJobStorageType.PostgreSql:
            {
                var options = UsePostgreSqlStorageOptions();
                configuration.UsePostgreSqlStorage(options.ConnectionString, options.StorageOptions);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected virtual PlatformHangfireUseSqlServerStorageOptions UseSqlServerStorageOptions()
    {
        return new PlatformHangfireUseSqlServerStorageOptions
        {
            ConnectionString = StorageOptionsConnectionString()
        };
    }

    protected virtual string StorageOptionsConnectionString()
    {
        return Configuration.GetConnectionString($"{DefaultHangfireBackgroundJobAppSettingsName}:ConnectionString");
    }

    protected virtual PlatformHangfireUseMongoStorageOptions UseMongoStorageOptions()
    {
        return new PlatformHangfireUseMongoStorageOptions
        {
            ConnectionString = StorageOptionsConnectionString()
        };
    }

    protected virtual PlatformHangfireUsePostgreSqlStorageOptions UsePostgreSqlStorageOptions()
    {
        return new PlatformHangfireUsePostgreSqlStorageOptions
        {
            ConnectionString = StorageOptionsConnectionString()
        };
    }
}
