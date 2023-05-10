using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

public class DemoDoSomeDomainEntityLogicActionOnSaveSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    public DemoDoSomeDomainEntityLogicActionOnSaveSnippetTextEntityEventHandler(ILoggerFactory loggerFactory, IUnitOfWorkManager unitOfWorkManager) : base(
        loggerFactory,
        unitOfWorkManager)
    {
    }

    // protected override bool ExecuteSeparatelyInBackgroundThread() => true;

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected override async Task HandleAsync(PlatformCqrsEntityEvent<TextSnippetEntity> @event, CancellationToken cancellationToken)
    {
        var encryptSnippetTextEvent = @event
            .FindDomainEvents<TextSnippetEntity.EncryptSnippetTextDomainEvent>()
            .FirstOrDefault();

        if (encryptSnippetTextEvent != null &&
            (@event.CrudAction == PlatformCqrsEntityEventCrudAction.Created || @event.CrudAction == PlatformCqrsEntityEventCrudAction.Updated))
        {
            // Demo handle domain entity event which is more specific than just CRUD operation
        }
    }
}
