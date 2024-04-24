using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.MessageBus.FreeFormatMessages;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseCommands.OtherDemos;

public sealed class DemoSendFreeFormatEventBusMessageCommand : PlatformCqrsCommand<DemoSendFreeFormatEventBusMessageCommandResult>
{
    public string Property1 { get; set; }
    public int Property2 { get; set; }
}

public sealed class DemoSendFreeFormatEventBusMessageCommandResult : PlatformCqrsCommandResult
{
}

internal sealed class DemoSendFreeFormatEventBusMessageCommandHandler
    : PlatformCqrsCommandApplicationHandler<DemoSendFreeFormatEventBusMessageCommand, DemoSendFreeFormatEventBusMessageCommandResult>
{
    private readonly IPlatformApplicationBusMessageProducer busMessageProducer;

    public DemoSendFreeFormatEventBusMessageCommandHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer busMessageProducer) : base(requestContextAccessor, unitOfWorkManager, cqrs, loggerFactory, rootServiceProvider)
    {
        this.busMessageProducer = busMessageProducer;
    }

    protected override async Task<DemoSendFreeFormatEventBusMessageCommandResult> HandleAsync(
        DemoSendFreeFormatEventBusMessageCommand request,
        CancellationToken cancellationToken)
    {
        await busMessageProducer.SendAsync(
            new DemoSendFreeFormatEventBusMessage
            {
                Property1 = request.Property1,
                Property2 = request.Property2
            },
            cancellationToken: cancellationToken);
        return new DemoSendFreeFormatEventBusMessageCommandResult();
    }
}
