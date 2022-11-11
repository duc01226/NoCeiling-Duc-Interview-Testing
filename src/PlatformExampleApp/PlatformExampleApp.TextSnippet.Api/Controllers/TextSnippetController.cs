using System.Threading;
using Easy.Platform.AspNetCore.Controllers;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Utils;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PlatformExampleApp.TextSnippet.Application.Caching;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;
using PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace PlatformExampleApp.TextSnippet.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TextSnippetController : PlatformBaseController
{
    public TextSnippetController(
        IPlatformCqrs cqrs,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        IConfiguration configuration) : base(cqrs, cacheRepositoryProvider, configuration)
    {
    }

    // GET: api/<TextSnippetController>
    [HttpGet]
    [Route("search")]
    public async Task<SearchSnippetTextQueryResult> Search([FromQuery] SearchSnippetTextQuery request)
    {
        // Random delay slow request for spinner
        Util.Random.DoByChance(50, () => Thread.Sleep(1000));

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
                requestKeyParts: new object[]
                {
                    nameof(Search), request
                });

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
        Util.Random.DoByChance(50, () => Thread.Sleep(1000));

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
    /// // Test get very big data stream to see data downloading streaming by return IEnumerable. Return data as stream using IEnumerable do not load all data into memory
    /// </summary>
    [HttpGet]
    [Route("testGetAllDataAsStream")]
    public async Task<IEnumerable<TextSnippetEntityDto>> TestGetAllDataAsStream()
    {
        return await Cqrs.SendQuery(new TestGetAllDataAsStreamQuery());
    }

    private static void RandomThrowToTestHandleInternalException()
    {
        Util.Random.DoByChance(
            percentChance: 5,
            () => throw new Exception("Random Test Throw Exception"));
    }
}
