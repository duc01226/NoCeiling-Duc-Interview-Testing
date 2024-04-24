using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Services;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseCommands.OtherDemos;

public sealed class DemoUseDemoDomainServiceCommand : PlatformCqrsCommand<DemoUseDemoDomainServiceCommandResult>
{
}

public sealed class DemoUseDemoDomainServiceCommandResult : PlatformCqrsCommandResult
{
    public TransferSnippetTextToMultiDbDemoEntityNameService.TransferSnippetTextToMultiDbDemoEntityNameResult
        TransferSnippetTextToMultiDbDemoEntityNameResult
    {
        get;
        set;
    }
}

internal sealed class DemoUseDemoDomainServiceCommandHandler
    : PlatformCqrsCommandApplicationHandler<DemoUseDemoDomainServiceCommand, DemoUseDemoDomainServiceCommandResult>
{
    // Demo use demoDomainService
    private readonly TransferSnippetTextToMultiDbDemoEntityNameService transferSnippetTextToMultiDbDemoEntityNameService;

    public DemoUseDemoDomainServiceCommandHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        TransferSnippetTextToMultiDbDemoEntityNameService transferSnippetTextToMultiDbDemoEntityNameService) : base(
        requestContextAccessor,
        unitOfWorkManager,
        cqrs,
        loggerFactory,
        rootServiceProvider)
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
