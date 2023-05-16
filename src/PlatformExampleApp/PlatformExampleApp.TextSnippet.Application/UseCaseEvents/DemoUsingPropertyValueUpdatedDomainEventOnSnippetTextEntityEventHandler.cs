using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

public class DemoUsingPropertyValueUpdatedDomainEventOnSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    public DemoUsingPropertyValueUpdatedDomainEventOnSnippetTextEntityEventHandler(ILoggerFactory loggerFactory, IUnitOfWorkManager unitOfWorkManager) : base(
        loggerFactory,
        unitOfWorkManager)
    {
    }

    // Default is true to improve performance when command save, the event is executed separately and could be in parallel.
    // Set it to false if you want the event executed sync with the command and in order
    // protected override bool AllowHandleInBackgroundThread() => false;

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected override async Task HandleAsync(PlatformCqrsEntityEvent<TextSnippetEntity> @event, CancellationToken cancellationToken)
    {
        // DEMO USING PROPERTY CHANGED DOMAIN EVENT
        var snippetTextPropUpdatedEvent = @event.FindPropertyValueUpdatedDomainEvent(p => p.SnippetText);
        if (snippetTextPropUpdatedEvent != null)
            CreateGlobalLogger()
                .LogInformation(
                    $"TextSnippetEntity Id:'{@event.EntityData.Id}' SnippetText updated. Prev: {snippetTextPropUpdatedEvent.OriginalValue}. New: {snippetTextPropUpdatedEvent.NewValue}");

        var fullTextPropUpdatedEvent = @event.FindPropertyValueUpdatedDomainEvent(p => p.FullText);
        if (fullTextPropUpdatedEvent != null)
            CreateGlobalLogger()
                .LogInformation(
                    $"TextSnippetEntity Id:'{@event.EntityData.Id}' FullText updated. Prev: {fullTextPropUpdatedEvent.OriginalValue}. New: {fullTextPropUpdatedEvent.NewValue}");
    }
}
