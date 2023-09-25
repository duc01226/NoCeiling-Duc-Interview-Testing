using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Queries;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Infrastructures.Caching;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

/// <summary>
/// // Test get very big data stream to see data downloading streaming by return IEnumerable. Return data as stream using IEnumerable do not load all data into memory
/// </summary>
public sealed class TestGetAllDataAsStreamQuery : PlatformCqrsQuery<TestGetAllDataAsStreamQueryResult>
{
}

public class TestGetAllDataAsStreamQueryResult
{
    public IAsyncEnumerable<TextSnippetEntityDto> AsyncEnumerableResult { get; set; }
    public IEnumerable<TextSnippetEntityDto> EnumerableResult { get; set; }
    public IEnumerable<TextSnippetEntityDto> EnumerableResultFromAsyncEnumerable { get; set; }
}

internal sealed class TestGetAllDataAsStreamQueryHandler : PlatformCqrsQueryApplicationHandler<TestGetAllDataAsStreamQuery, TestGetAllDataAsStreamQueryResult>
{
    private readonly ITextSnippetRepository<TextSnippetEntity> textSnippetRepository;

    public TestGetAllDataAsStreamQueryHandler(
        IPlatformApplicationUserContextAccessor userContext,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider,
        ITextSnippetRepository<TextSnippetEntity> textSnippetRepository) : base(userContext, loggerFactory, rootServiceProvider, cacheRepositoryProvider)
    {
        this.textSnippetRepository = textSnippetRepository;
    }

    [SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
    protected override async Task<TestGetAllDataAsStreamQueryResult> HandleAsync(TestGetAllDataAsStreamQuery request, CancellationToken cancellationToken)
    {
        // Test get very big data stream to see data downloading streaming by return IAsyncEnumerable.
        // Return data as stream using IAsyncEnumerable do not load all data or sub list of data into memory, it stream each item async
        var asyncEnumerableResult = Enumerable.Range(0, 10000)
            .SelectManyAsync(
                p => textSnippetRepository.GetAllAsyncEnumerable(queryBuilder: query => query).Select(p => new TextSnippetEntityDto(p)));

        // Test use enumerable to see the memory different
        var enumerableResult = GetEnumerableResult();
        var enumerableResultFromAsyncEnumerable = GetEnumerableResultFromAsyncEnumerable();

        var (demoOtherNormalParallelRequestUsingOnceTimeUow1, demoOtherNormalParallelRequestUsingOnceTimeUow2, demoOtherNormalParallelRequestUsingOnceTimeUow3) =
            await Util.TaskRunner.WhenAll(
                textSnippetRepository.CountAsync(cancellationToken: cancellationToken),
                textSnippetRepository.CountAsync(cancellationToken: cancellationToken),
                textSnippetRepository.FirstOrDefaultAsync(cancellationToken: cancellationToken));

        return new TestGetAllDataAsStreamQueryResult
        {
            AsyncEnumerableResult = asyncEnumerableResult,
            EnumerableResult = enumerableResult,
            EnumerableResultFromAsyncEnumerable = enumerableResultFromAsyncEnumerable
        };
    }

    private IEnumerable<TextSnippetEntityDto> GetEnumerableResult()
    {
        return Enumerable.Range(0, 10000).SelectMany(i => textSnippetRepository.GetAllEnumerable().Select(p => new TextSnippetEntityDto(p)));
    }

    private IEnumerable<TextSnippetEntityDto> GetEnumerableResultFromAsyncEnumerable()
    {
        return Enumerable.Range(0, 10000)
            .SelectManyAsync(
                p => textSnippetRepository.GetAllAsyncEnumerable(queryBuilder: query => query).Select(p => new TextSnippetEntityDto(p)))
            .ToEnumerable();
    }
}
