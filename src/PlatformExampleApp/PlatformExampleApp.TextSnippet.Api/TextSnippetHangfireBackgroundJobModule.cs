using System.Data.SqlClient;
using Easy.Platform.HangfireBackgroundJob;
using Easy.Platform.Infrastructures.BackgroundJob;
using Easy.Platform.Persistence;
using Hangfire;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Npgsql;
using PlatformExampleApp.TextSnippet.Application;

namespace PlatformExampleApp.TextSnippet.Api;

public class TextSnippetHangfireBackgroundJobModule : PlatformHangfireBackgroundJobModule
{
    public TextSnippetHangfireBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) :
        base(serviceProvider, configuration)
    {
    }

    public override bool AutoUseDashboardUi => true;

    protected override PlatformHangfireBackgroundJobStorageType UseBackgroundJobStorage()
    {
        return Configuration.GetSection("UseDbType")
            .Get<string>()
            .WhenValue("MongoDb", _ => PlatformHangfireBackgroundJobStorageType.Mongo)
            .WhenValue("Postgres", _ => PlatformHangfireBackgroundJobStorageType.PostgreSql)
            .Else(_ => PlatformHangfireBackgroundJobStorageType.Sql)
            .Execute();
    }

    protected override string StorageOptionsConnectionString()
    {
        return Configuration.GetSection("UseDbType")
            .Get<string>()
            .WhenValue(
                "MongoDb",
                _ => new MongoUrlBuilder(Configuration.GetSection("MongoDB:ConnectionString").Value)
                    .With(
                        p => p.MinConnectionPoolSize = PlatformPersistenceModule.RecommendedMinPoolSize) // Always available connection to serve request, reduce latency
                    .With(
                        p => p.MaxConnectionPoolSize = PlatformPersistenceModule.RecommendedMaxPoolSize)
                    .With(p => p.MaxConnectionIdleTime = PlatformPersistenceModule.RecommendedConnectionIdleLifetimeSeconds.Seconds())
                    .With(p => p.ConnectTimeout = 30.Seconds())
                    .ToString())
            .WhenValue(
                "Postgres",
                _ => new NpgsqlConnectionStringBuilder(Configuration.GetConnectionString("PostgreSqlConnection"))
                    .With(conn => conn.Enlist = false)
                    .With(conn => conn.Pooling = true)
                    .With(conn => conn.MinPoolSize = PlatformPersistenceModule.RecommendedMinPoolSize) // Always available connection to serve request, reduce latency
                    .With(conn => conn.MaxPoolSize = PlatformPersistenceModule.RecommendedMaxPoolSize) // Setup based on app resource cpu ram max concurrent
                    .With(conn => conn.Timeout = 30)
                    .With(conn => conn.ConnectionIdleLifetime = PlatformPersistenceModule.RecommendedConnectionIdleLifetimeSeconds)
                    .With(conn => conn.ConnectionPruningInterval = PlatformPersistenceModule.RecommendedConnectionIdleLifetimeSeconds)
                    .ToString())
            .Else(
                _ => new SqlConnectionStringBuilder(Configuration.GetConnectionString("DefaultConnection"))
                    .With(conn => conn.Enlist = false)
                    .With(conn => conn.LoadBalanceTimeout = PlatformPersistenceModule.RecommendedConnectionIdleLifetimeSeconds) // (I)
                    .With(conn => conn.Pooling = true)
                    .With(conn => conn.MinPoolSize = PlatformPersistenceModule.RecommendedMinPoolSize) // Always available connection to serve request, reduce latency
                    .With(conn => conn.MaxPoolSize = PlatformPersistenceModule.RecommendedMaxPoolSize) // Setup based on app resource cpu ram max concurrent
                    .With(p => p.ConnectTimeout = 30)
                    .ToString())
            .Execute();
    }

    protected override PlatformHangfireUseMongoStorageOptions UseMongoStorageOptions()
    {
        return base.UseMongoStorageOptions()
            .With(o => o.DatabaseName = Configuration.GetSection("MongoDB:Database").Get<string>());
    }

    protected override BackgroundJobServerOptions BackgroundJobServerOptionsConfigure(IServiceProvider provider, BackgroundJobServerOptions options)
    {
        return base.BackgroundJobServerOptionsConfigure(provider, options)
            .With(s => s.WorkerCount = Configuration.GetValue<int?>("PostgreSql:WorkerCount") ?? TextSnippetApplicationConstants.DefaultBackgroundJobWorkerCount);
    }

    protected override PlatformBackgroundJobUseDashboardUiOptions BackgroundJobUseDashboardUiOptions()
    {
        return base.BackgroundJobUseDashboardUiOptions()
            .With(o => o.UseAuthentication = true)
            .With(
                o => o.BasicAuthentication = new PlatformBackgroundJobUseDashboardUiOptions.BasicAuthentications
                {
                    UserName = Configuration.GetValue<string>("BackgroundJob:DashboardUiOptions:BasicAuthentication:UserName"),
                    Password = Configuration.GetValue<string>("BackgroundJob:DashboardUiOptions:BasicAuthentication:Password")
                });
    }
}
