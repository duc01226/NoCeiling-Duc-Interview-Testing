using Easy.Platform.AspNetCore.Controllers;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.Persistence;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands.OtherDemos;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;
using PlatformExampleApp.TextSnippet.Domain.Entities;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PlatformExampleApp.TextSnippet.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TextSnippetController : PlatformBaseController
{
    private readonly ITextSnippetDbContext textSnippetDbContext;

    public TextSnippetController(
        IPlatformCqrs cqrs,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        IConfiguration configuration,
        ITextSnippetDbContext textSnippetDbContext) : base(cqrs, cacheRepositoryProvider, configuration)
    {
        this.textSnippetDbContext = textSnippetDbContext;
    }

    // GET: api/<TextSnippetController>
    [HttpGet]
    [Route("search")]
    public async Task<SearchSnippetTextQueryResult> Search([FromQuery] SearchSnippetTextQuery request)
    {
        // Random delay slow request for spinner
        Util.RandomGenerator.DoByChance(50, () => Thread.Sleep(1000));

        RandomThrowToTestHandleInternalException();

        // Using default last registered cache repository (default is built-in memory cache).
        //return await CacheRepositoryProvider.GetCollection<TextSnippetApplicationCollectionCacheKeyProvider>()
        //    .CacheRequestAsync(
        //        () => Cqrs.SendQuery(request),
        //        new object[] { nameof(Search), request },
        //        new TextSnippetConfigurationCollectionCacheEntryOptions(Configuration));

        // Test case use default CacheEntryOptions. Could be configured via override DefaultPlatformCacheEntryOptions in module
        return await CacheRepositoryProvider.GetCollection<TextSnippetCollectionCacheKeyProvider>()
            .CacheRequestAsync(
                () => Cqrs.SendQuery(request),
                requestKeyParts: SearchSnippetTextQuery.BuildCacheRequestKeyParts(request, userId: null, companyId: null));

        // Using distributed cache and also use CacheRequestUseConfigOptionsAsync for convenient
        //return await CacheRepositoryProvider.GetCollection<TextSnippetCollectionCacheKeyProvider>(PlatformCacheRepositoryType.Distributed)
        //    .CacheRequestUseConfigOptionsAsync<TextSnippetCollectionConfigurationCacheEntryOptions, SearchSnippetTextQueryResult>(
        //        () => Cqrs.SendQuery(request),
        //        new object[] { nameof(Search), request });
    }

    // POST api/<TextSnippetController>
    [HttpPost]
    [Route("save")]
    public async Task<SaveSnippetTextCommandResult> Save([FromBody] SaveSnippetTextCommand request)
    {
        // Random delay slow request for spinner
        Util.RandomGenerator.DoByChance(50, () => Thread.Sleep(1000));

        RandomThrowToTestHandleInternalException();

        return await Cqrs.SendCommand(request);
    }

    [HttpGet]
    [Route("demoScheduleBackgroundJobManuallyCommand")]
    public async Task<DemoScheduleBackgroundJobManuallyCommandResult> DemoScheduleBackgroundJobManuallyCommand(
        string updateTextSnippetFullText = "")
    {
        return await Cqrs.SendCommand(
            new DemoScheduleBackgroundJobManuallyCommand
            {
                NewSnippetText = updateTextSnippetFullText
            });
    }

    [HttpGet]
    [Route("DemoUseDemoDomainServiceCommand")]
    public async Task<DemoUseDemoDomainServiceCommandResult> DemoUseDemoDomainServiceCommand()
    {
        return await Cqrs.SendCommand(
            new DemoUseDemoDomainServiceCommand());
    }

    [HttpPost]
    [Route("demoSendFreeFormatEventBusMessageCommand")]
    public async Task<DemoSendFreeFormatEventBusMessageCommandResult> DemoSendFreeFormatEventBusMessageCommand(
        [FromBody] DemoSendFreeFormatEventBusMessageCommand request)
    {
        return await Cqrs.SendCommand(request);
    }

    [HttpGet]
    [Route("testHandleInternalException")]
    public Task TestHandleInternalException()
    {
        throw new Exception("TestLoggingForInternalException");
    }

    /// <summary>
    /// Test get very big data stream to see data downloading streaming by return IAsyncEnumerable. Return data as stream using IAsyncEnumerable do not load all data into memory
    /// </summary>
    [HttpGet]
    [Route("testGetAllDataAsIAsyncEnumerableStream")]
    public async Task<IActionResult> TestGetAllDataAsIAsyncEnumerableStream()
    {
        var result = await Cqrs.SendQuery(new TestGetAllDataAsStreamQuery()).Then(p => p.AsyncEnumerableResult);
        return Ok(result);
    }

    /// <summary>
    /// // Test get very big data as IEnumerable to see memory issues compared to IAsyncEnumerable
    /// </summary>
    [HttpGet]
    [Route("testGetAllDataAsIEnumerableStream")]
    public async Task<IActionResult> TestGetAllDataAsIEnumerableStream()
    {
        var result = await Cqrs.SendQuery(new TestGetAllDataAsStreamQuery()).Then(p => p.EnumerableResult);
        return Ok(result);
    }

    /// <summary>
    /// // Test get very big data as IEnumerable to see memory issues compared to IAsyncEnumerable
    /// </summary>
    [HttpGet]
    [Route("testGetAllDataAsIEnumerableFromAsyncEnumerableStream")]
    public async Task<IActionResult> TestGetAllDataAsIEnumerableFromAsyncEnumerableStream()
    {
        var result = await Cqrs.SendQuery(new TestGetAllDataAsStreamQuery()).Then(p => p.EnumerableResultFromAsyncEnumerable);
        return Ok(result);
    }

    [HttpPost]
    [Route("testSaveUsingDirectDbContext")]
    public async Task<SaveSnippetTextCommandResult> TestSaveUsingDirectDbContext([FromBody] SaveSnippetTextCommand request)
    {
        var savedEntity = await textSnippetDbContext.CreateOrUpdateAsync<TextSnippetEntity, string>(request.Data.MapToEntity());

        await textSnippetDbContext.SaveChangesAsync();

        return new SaveSnippetTextCommandResult
        {
            SavedData = new TextSnippetEntityDto(savedEntity)
        };
    }

    [HttpGet]
    [Route("DemoUseCreateOrUpdateMany")]
    public async Task<DemoUseCreateOrUpdateManyCommandResult> DemoUseCreateOrUpdateMany([FromQuery] DemoUseCreateOrUpdateManyCommand request)
    {
        var result = await Cqrs.SendCommand(request);

        return result;
    }

    //DemoUseCreateOrUpdateManyCommand

    private void RandomThrowToTestHandleInternalException(int percentChance = 5)
    {
        if (Configuration.GetSection("RandomThrowExceptionForTesting").Get<bool?>() == true)
            Util.RandomGenerator.DoByChance(
                percentChance,
                () => throw new Exception("Random Test Throw Exception"));
    }


    [HttpGet]
    [Route("TestIAsyncEnumerable")]
    public IAsyncEnumerable<string> TestIAsyncEnumerable()
    {
        return GetAsyncContent();
    }

    private async IAsyncEnumerable<string> GetAsyncContent()
    {
        for (var i = 0; i < int.MaxValue; i++)
            yield return await Task.Run(() => Ulid.NewUlid().ToString());
    }
}
