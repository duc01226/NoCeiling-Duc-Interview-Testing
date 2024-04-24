using Easy.Platform.Application.BackgroundJob;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.Shared.Application.MessageBus.FreeFormatMessages;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands.OtherDemos;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Application.BackgroundJob;

[PlatformRecurringJob("0 0 * * *", timeZoneOffset: -7)]
public sealed class TestRecurringBackgroundJobExecutor : PlatformApplicationBackgroundJobExecutor
{
    private readonly IPlatformApplicationBusMessageProducer busMessageProducer;
    private readonly IPlatformCqrs cqrs;
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository;

    public TestRecurringBackgroundJobExecutor(
        ILoggerFactory loggerFactory,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IPlatformRootServiceProvider rootServiceProvider,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository,
        IPlatformCqrs cqrs,
        IPlatformApplicationBusMessageProducer busMessageProducer) : base(unitOfWorkManager, loggerFactory, rootServiceProvider)
    {
        this.textSnippetEntityRepository = textSnippetEntityRepository;
        this.cqrs = cqrs;
        this.busMessageProducer = busMessageProducer;
    }

    public override async Task ProcessAsync(object param)
    {
        await textSnippetEntityRepository.CreateOrUpdateAsync(
            TextSnippetEntity.Create(
                id: Guid.Parse("76e0f523-ee53-4124-b109-13dedaa4618d"),
                snippetText: "TestRecurringBackgroundJob " + Clock.Now.ToShortTimeString(),
                fullText: "Test of recurring job upsert this entity"));

        await cqrs.SendCommand(
            new DemoSendFreeFormatEventBusMessageCommand
            {
                Property1 = "TestRecurringBackgroundJobExecutor Prop1"
            });

        await busMessageProducer.SendAsync(
            new TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks());
        await busMessageProducer.SendAsync(
            new TestFreeFormatMessageInDifferentSharedAssemblyCheckingOutboxResolveWorks1());
    }
}
