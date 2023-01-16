using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using PlatformExampleApp.TextSnippet.Domain.Services;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseCommands;

public class DemoUseDemoDomainServiceCommand : PlatformCqrsCommand<DemoUseDemoDomainServiceCommandResult>
{
}

public class DemoUseDemoDomainServiceCommandResult : PlatformCqrsCommandResult
{
    public TransferSnippetTextToMultiDbDemoEntityNameService.TransferSnippetTextToMultiDbDemoEntityNameResult
        TransferSnippetTextToMultiDbDemoEntityNameResult
    {
        get;
        set;
    }
}

public class DemoUseDemoDomainServiceCommandHandler : PlatformCqrsCommandApplicationHandler<DemoUseDemoDomainServiceCommand, DemoUseDemoDomainServiceCommandResult>
{
    // Demo use demoDomainService
    private readonly TransferSnippetTextToMultiDbDemoEntityNameService transferSnippetTextToMultiDbDemoEntityNameService;

    public DemoUseDemoDomainServiceCommandHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        TransferSnippetTextToMultiDbDemoEntityNameService transferSnippetTextToMultiDbDemoEntityNameService) : base(userContext, unitOfWorkManager, cqrs)
    {
        this.transferSnippetTextToMultiDbDemoEntityNameService = transferSnippetTextToMultiDbDemoEntityNameService;
    }

    protected override async Task<DemoUseDemoDomainServiceCommandResult> HandleAsync(
        DemoUseDemoDomainServiceCommand request,
        CancellationToken cancellationToken)
    {
        var transferSnippetTextToMultiDbDemoEntityNameResult =
            await transferSnippetTextToMultiDbDemoEntityNameService.Execute();

        return new DemoUseDemoDomainServiceCommandResult
        {
            TransferSnippetTextToMultiDbDemoEntityNameResult = transferSnippetTextToMultiDbDemoEntityNameResult
        };
    }
}
