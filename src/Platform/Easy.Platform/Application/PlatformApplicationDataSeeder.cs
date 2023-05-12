using Easy.Platform.Common;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Configuration;

namespace Easy.Platform.Application;

public interface IPlatformApplicationDataSeeder
{
    /// <summary>
    /// Seed order support multiple data seeders seed by order. Start default value from 0; Higher order will execute later
    /// </summary>
    public int SeedOrder { get; }

    /// <summary>
    /// Default value is 0 mean that No Seeding in background; <br />
    /// When value is > 0, Support delay execute a seed data task in background thread.
    /// This is needed if you want to seed a lot of data for testing performance purpose or you seed by command so wait for
    /// infrastructure to started,
    /// so you don't prevent the application to kick start, and also waiting for all other micro services could started
    /// before you do seed data, to ensure that other services still may receive data if they sync (listen data via message
    /// bus)
    /// </summary>
    public int DelaySeedingInBackgroundBySeconds { get; }

    public Task SeedData(bool isReplaceNewSeedData = false);
}

/// <summary>
/// The data seeders will run SeedData on module.Init()
/// </summary>
public abstract class PlatformApplicationDataSeeder : IPlatformApplicationDataSeeder
{
    protected readonly IConfiguration Configuration;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationDataSeeder(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        UnitOfWorkManager = unitOfWorkManager;
        ServiceProvider = serviceProvider;
        Configuration = configuration;
    }

    public static int DefaultSeedingMinimumDummyItemsCount => PlatformEnvironment.IsDevelopment ? 1000 : 100000;

    /// <summary>
    /// Default value is SeedingMinimumDummyItemsCount; <br/>
    /// Used to read SeedingMinimumDummyItemsCount in appsettings by Configuration. <br/>
    /// Could update it to change the configuration key.
    /// </summary>
    public static string SeedingMinimumDummyItemsCountConfigurationKey { get; set; } = "SeedingMinimumDummyItemsCount";

    public static int SeedingMinimumDummyItemsCount =>
        PlatformGlobal.Configuration.GetValue<int?>(SeedingMinimumDummyItemsCountConfigurationKey) ??
        DefaultSeedingMinimumDummyItemsCount;

    public static int DefaultActiveDelaySeedingInBackgroundBySeconds => PlatformEnvironment.IsDevelopment ? 30 : 180;
    public static int DefaultDelayRetryCheckSeedDataBySeconds => PlatformEnvironment.IsDevelopment ? 2 : 4;
    public static int DefaultMaxWaitSeedDataBySyncMessagesBySeconds => PlatformEnvironment.IsDevelopment ? 300 : 600;

    /// <summary>
    /// Default is true. Override this if you want to start uow yourself or not want to
    /// auto run in a uow
    /// </summary>
    protected virtual bool AutoBeginUow => false;

    public virtual async Task SeedData(bool isReplaceNewSeedData = false)
    {
        if (AutoBeginUow)
            using (var uow = UnitOfWorkManager.Begin())
            {
                await InternalSeedData();
                await uow.CompleteAsync();
            }
        else
            await InternalSeedData();
    }

    public virtual int SeedOrder => 0;

    public virtual int DelaySeedingInBackgroundBySeconds => 0;

    protected abstract Task InternalSeedData(bool isReplaceNewSeed = false);
}
