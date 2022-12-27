using System.Linq.Expressions;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Queries;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Common.Dtos;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Persistence.Services;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.Helpers;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseQueries;

public class SearchSnippetTextQuery : PlatformCqrsPagedQuery<SearchSnippetTextQueryResult, TextSnippetEntityDto>
{
    public string SearchText { get; set; }
    public Guid? SearchId { get; set; }
    public string SearchAddress { get; set; }
    public string SearchAddressString { get; set; }
    public string SearchSingleAddress { get; set; }
}

public class SearchSnippetTextQueryResult : PlatformCqrsQueryPagedResult<TextSnippetEntityDto>
{
    public SearchSnippetTextQueryResult(List<TextSnippetEntityDto> items, long totalCount, IPlatformPagedRequest pagedRequest) : base(items, totalCount, pagedRequest)
    {
    }
}

public class SearchSnippetTextQueryHandler : PlatformCqrsQueryApplicationHandler<SearchSnippetTextQuery, SearchSnippetTextQueryResult>
{
    // This is just a demo that helper is used by Application Commands/Queries
    // ReSharper disable once NotAccessedField.Local
    private readonly ExampleHelper exampleHelper;

    private readonly IPlatformFullTextSearchPersistenceService fullTextSearchPersistenceService;

    // If get default repository/unitOfWork will get from the latest registered module. See TextSnippetApplicationModule.
    private readonly ITextSnippetRepository<TextSnippetEntity> repository;

    public SearchSnippetTextQueryHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        ITextSnippetRepository<TextSnippetEntity> repository,
        IPlatformFullTextSearchPersistenceService fullTextSearchPersistenceService,
        ExampleHelper exampleHelper) : base(userContext, unitOfWorkManager)
    {
        this.repository = repository;
        this.fullTextSearchPersistenceService = fullTextSearchPersistenceService;
        this.exampleHelper = exampleHelper;
    }

    protected override async Task<SearchSnippetTextQueryResult> HandleAsync(
        SearchSnippetTextQuery request,
        CancellationToken cancellationToken)
    {
        // NOT RELATED TO MAIN LOGIC. DEMO HOW TO JOIN QUERY
        //var joinQuery = await repository.GetAllAsync(
        //    queryBuilder: (uow, query) => query.Join(
        //        multiDbDemoEntityRepository.GetAllQuery(uow),
        //        p => p.Id,
        //        p => p.Id,
        //        (entity, demoEntity) => entity),
        //    cancellationToken: cancellationToken);

        // STEP 1: Build Queries
        var fullItemsQueryBuilder = repository.GetQueryBuilder(
            query => query
                .PipeIf(
                    !string.IsNullOrEmpty(request.SearchText),
                    _ => fullTextSearchPersistenceService.Search(
                        query,
                        request.SearchText,
                        inFullTextSearchProps: new Expression<Func<TextSnippetEntity, object>>[]
                        {
                            e => e.SnippetText
                        },
                        fullTextExactMatch: true,
                        includeStartWithProps: new Expression<Func<TextSnippetEntity, object>>[]
                        {
                            e => e.SnippetText
                        }))
                .PipeIf(
                    !string.IsNullOrEmpty(request.SearchAddress),
                    _ => _.Where(p => p.Addresses.Any(add => add.Street == request.SearchAddress)))
                .PipeIf(
                    !string.IsNullOrEmpty(request.SearchSingleAddress),
                    _ => _.Where(p => p.Address.Street == request.SearchSingleAddress))
                .PipeIf(
                    !string.IsNullOrEmpty(request.SearchAddressString),
                    _ => _.Where(p => p.AddressStrings.Any() && p.AddressStrings.Contains(request.SearchAddressString)))
                .WhereIf(request.SearchId != null, p => p.Id == request.SearchId));

        // STEP 2: Get Data
        var (pagedEntities, totalCount) = await Util.TaskRunner.WhenAll(
            repository.GetAllAsync(
                query => fullItemsQueryBuilder(query)
                    .OrderByDescending(p => p.CreatedDate)
                    .PipeIf(
                        request.IsPagedRequestValid(),
                        query => query.PageBy(request.SkipCount, request.MaxResultCount)),
                cancellationToken),
            repository.CountAsync(fullItemsQueryBuilder, cancellationToken));

        // STEP 3: Build and return result
        return new SearchSnippetTextQueryResult(
            pagedEntities.Select(p => new TextSnippetEntityDto(p)).ToList(),
            totalCount,
            request);
    }
}
