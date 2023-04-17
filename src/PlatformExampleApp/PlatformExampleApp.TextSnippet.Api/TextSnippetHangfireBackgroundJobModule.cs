using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;
using Easy.Platform.HangfireBackgroundJob;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Api;

public class TextSnippetHangfireBackgroundJobModule : PlatformHangfireBackgroundJobModule
{
    public TextSnippetHangfireBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) :
        base(serviceProvider, configuration)
    {
    }

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
}
