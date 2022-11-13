using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Queries;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Domain.UnitOfWork;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

/// <summary>
/// // Test get very big data stream to see data downloading streaming by return IEnumerable. Return data as stream using IEnumerable do not load all data into memory
/// </summary>
public class TestGetAllDataAsStreamQuery : PlatformCqrsQuery<IEnumerable<TextSnippetEntityDto>>
{
}

public class TestGetAllDataAsStreamQueryHandler : PlatformCqrsQueryApplicationHandler<TestGetAllDataAsStreamQuery, IEnumerable<TextSnippetEntityDto>>
{
    private readonly ITextSnippetRepository<TextSnippetEntity> textSnippetRepository;

    public TestGetAllDataAsStreamQueryHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        ITextSnippetRepository<TextSnippetEntity> textSnippetRepository) : base(userContext, unitOfWorkManager)
    {
        this.textSnippetRepository = textSnippetRepository;
    }

    protected override async Task<IEnumerable<TextSnippetEntityDto>> HandleAsync(TestGetAllDataAsStreamQuery request, CancellationToken cancellationToken)
    {
        var result = Enumerable.Range(0, 10000).Aggregate(GetDataFn(), (items, i) => items.Concat(GetDataFn()));

        // GetGlobalUowQuery use it's own UOW. Could call it in parallel with others
        // normal get data. Couldn't run more than two GetGlobalUowQuery().ToList() or First() get data in parallel
        // because they run in on the same uow
        var (demoOtherNormalParallelRequestUsingOnceTimeUow1, demoOtherNormalParallelRequestUsingOnceTimeUow2, demoOtherNormalParallelRequestUsingOnceTimeUow3) =
            await Util.TaskRunner.WhenAll(
                textSnippetRepository.CountAsync(cancellationToken: cancellationToken),
                textSnippetRepository.CountAsync(cancellationToken: cancellationToken),
                textSnippetRepository.FirstOrDefaultAsync(textSnippetRepository.GetGlobalUowQuery(), cancellationToken));

        return result;

        // Test get very big data stream to see data downloading streaming by return IEnumerable. Return data as stream using IEnumerable do not load all data into memory
        IEnumerable<TextSnippetEntityDto> GetDataFn()
        {
            return textSnippetRepository.GetGlobalUowQuery().AsEnumerable().Select(p => new TextSnippetEntityDto(p));
        }
    }
}
