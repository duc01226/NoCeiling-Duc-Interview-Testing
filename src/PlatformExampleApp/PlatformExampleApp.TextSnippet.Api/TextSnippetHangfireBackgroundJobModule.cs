using Easy.Platform.HangfireBackgroundJob;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Microsoft.Extensions.Configuration;
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
            .WhenValue("MongoDb", _ => Configuration.GetSection("MongoDB:ConnectionString").Get<string>())
            .WhenValue("Postgres", _ => Configuration.GetSection("ConnectionStrings:PostgreSqlConnection").Get<string>())
            .Else(_ => Configuration.GetConnectionString("DefaultConnection"))
            .Execute();
    }

    protected override PlatformHangfireUseMongoStorageOptions UseMongoStorageOptions()
    {
        return base.UseMongoStorageOptions()
            .With(_ => _.DatabaseName = Configuration.GetSection("MongoDB:Database").Get<string>());
    }

    protected override BackgroundJobServerOptions BackgroundJobServerOptionsConfigure(IServiceProvider provider, BackgroundJobServerOptions options)
    {
        return base.BackgroundJobServerOptionsConfigure(provider, options)
            .With(_ => _.WorkerCount = Configuration.GetValue<int?>("PostgreSql:WorkerCount") ?? TextSnippetApplicationConstants.DefaultBackgroundJobWorkerCount);
    }

    protected override PlatformBackgroundJobUseDashboardUiOptions BackgroundJobUseDashboardUiOptions()
    {
        return base.BackgroundJobUseDashboardUiOptions().With(_ => _.UseAuthentication = true).With(
            _ => _.BasicAuthentication = new PlatformBackgroundJobUseDashboardUiOptions.BasicAuthentications
            {
                UserName = Configuration.GetValue<string>("BackgroundJob:DashboardUiOptions:BasicAuthentication:UserName"),
                Password = Configuration.GetValue<string>("BackgroundJob:DashboardUiOptions:BasicAuthentication:Password")
            });
    }
}
