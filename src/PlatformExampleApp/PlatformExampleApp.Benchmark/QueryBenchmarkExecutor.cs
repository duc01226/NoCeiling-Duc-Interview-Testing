using BenchmarkDotNet.Attributes;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformExampleApp.TextSnippet.Api;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

namespace PlatformExampleApp.Benchmark;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class QueryBenchmarkExecutor
{
    public QueryBenchmarkExecutor()
    {
        Configuration = PlatformConfigurationBuilder.GetConfigurationBuilder().Build();

        Services = ConfigureServices(Configuration);

        ServiceProvider = Services.BuildServiceProvider();
    }

    protected IConfigurationRoot Configuration { get; set; }
    protected IServiceCollection Services { get; set; }
    protected IServiceProvider ServiceProvider { get; set; }

    private static ServiceCollection ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.Register(sp => configuration, ServiceLifeTime.Singleton);
        services.RegisterModule<TextSnippetApiAspNetCoreModule>();

        return services;
    }

    [Benchmark]
    public async Task<SearchSnippetTextQueryResult> GetEmployeeWithTimeLogsListQuery()
    {
        return await ServiceProvider
            .ExecuteInjectScopedAsync<SearchSnippetTextQueryResult>(
                async (IPlatformCqrs cqrs, IPlatformApplicationRequestContextAccessor requestContextAccessor, IConfiguration configuration) =>
                {
                    PopulateMockBenchmarkRequestContext(requestContextAccessor.Current, configuration);

                    return await cqrs.SendQuery(new SearchSnippetTextQuery());
                });
    }

    private void PopulateMockBenchmarkRequestContext(IPlatformApplicationRequestContext current, IConfiguration configuration)
    {
        current.SetEmail("testBenchmark@example.com");
    }
}
