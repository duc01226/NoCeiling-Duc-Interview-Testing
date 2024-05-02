using BenchmarkDotNet.Attributes;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.EfCore.Logging.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformExampleApp.TextSnippet.Api;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;
using Serilog;

namespace PlatformExampleApp.Benchmark;

[MemoryDiagnoser(false)]
public class QueryBenchmarkExecutor
{
    public QueryBenchmarkExecutor()
    {
        Configuration = PlatformConfigurationBuilder.GetConfigurationBuilder().Build();

        Services = ConfigureServices(Configuration);

        ServiceProvider = Services.BuildServiceProvider();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(Configuration)
            .EnrichDefaultPlatformEnrichers()
            .WithExceptionDetails(p => p.WithPlatformEfCoreExceptionDetailsDestructurers())
            .CreateLogger();

        Log.Logger.Information("------------------------SETUP INFO - START------------------------");
        Log.Logger.Information("[INFO] ParallelTestingCount:{ParallelTestingCount}", ParallelTestingCount());
        Log.Logger.Information("------------------------SETUP INFO - END  ------------------------");
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

    protected int ParallelTestingCount()
    {
        return Environment.ProcessorCount >= 2 ? Environment.ProcessorCount / 2 : 1;
    }

    [Benchmark]
    public async Task<List<SearchSnippetTextQueryResult>> GetEmployeeWithTimeLogsListQuery()
    {
        return await Util.TaskRunner.WhenAll(
            ParallelTestingCount()
                .ToRange()
                .Select(
                    p => ServiceProvider
                        .ExecuteInjectScopedAsync<SearchSnippetTextQueryResult>(
                            async (IPlatformCqrs cqrs, IPlatformApplicationRequestContextAccessor requestContextAccessor, IConfiguration configuration) =>
                            {
                                PopulateMockBenchmarkRequestContext(requestContextAccessor.Current, configuration);

                                return await cqrs.SendQuery(new SearchSnippetTextQuery());
                            })));
    }

    [Benchmark]
    public Type? GetRegisteredPlatformModuleAssembliesType()
    {
        return ServiceProvider.GetRequiredService<IPlatformRootServiceProvider>()
            .GetRegisteredPlatformModuleAssembliesType("Easy.Platform.Application.Cqrs.Events.InboxSupport.PlatformCqrsEventInboxBusMessageConsumer");
    }

    [Benchmark]
    public Type? GetRegisteredPlatformModuleAssembliesType_NoCache()
    {
        var scanAssemblies = ServiceProvider.GetServices<PlatformModule>()
            .SelectMany(p => p.GetServicesRegisterScanAssemblies())
            .ConcatSingle(typeof(PlatformModule).Assembly)
            .ToList();

        var scannedResultType = scanAssemblies
            .Select(p => p.GetType("Easy.Platform.Application.Cqrs.Events.InboxSupport.PlatformCqrsEventInboxBusMessageConsumer"))
            .FirstOrDefault(p => p != null)
            .Pipe(
                scannedResultType => scannedResultType ?? Type.GetType(
                    "Easy.Platform.Application.Cqrs.Events.InboxSupport.PlatformCqrsEventInboxBusMessageConsumer",
                    throwOnError: false));

        return scannedResultType;
    }

    private void PopulateMockBenchmarkRequestContext(IPlatformApplicationRequestContext current, IConfiguration configuration)
    {
        current.SetEmail("testBenchmark@example.com");
    }
}
