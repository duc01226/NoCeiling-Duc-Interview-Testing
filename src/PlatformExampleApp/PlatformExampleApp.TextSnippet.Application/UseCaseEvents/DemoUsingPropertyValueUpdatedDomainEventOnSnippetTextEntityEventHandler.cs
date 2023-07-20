using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

internal sealed class DemoUsingFieldUpdatedDomainEventOnSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    public DemoUsingFieldUpdatedDomainEventOnSnippetTextEntityEventHandler(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider)
    {
    }

    // Default is true to improve performance when command save, the event is executed separately and could be in parallel.
    // Set it to false if you want the event executed sync with the command and in order
    // protected override bool AllowHandleInBackgroundThread() => false;

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    protected override async Task HandleAsync(PlatformCqrsEntityEvent<TextSnippetEntity> @event, CancellationToken cancellationToken)
    {
        // Test slow event do not affect main command
        await Task.Delay(5.Seconds(), cancellationToken);

        // DEMO USING PROPERTY CHANGED DOMAIN EVENT
        var snippetTextPropUpdatedEvent = @event.FindFieldUpdatedEvent(p => p.SnippetText);
        if (snippetTextPropUpdatedEvent != null)
            CreateGlobalLogger()
                .LogInformation(
                    "TextSnippetEntity Id:'{EntityDataId}' SnippetText updated. Prev: {OriginalValue}. New: {NewValue}",
                    @event.EntityData.Id,
                    snippetTextPropUpdatedEvent.OriginalValue,
                    snippetTextPropUpdatedEvent.NewValue);

        var fullTextPropUpdatedEvent = @event.FindFieldUpdatedEvent(p => p.FullText);
        if (fullTextPropUpdatedEvent != null)
            CreateGlobalLogger()
                .LogInformation(
                    "TextSnippetEntity Id:'{Id}' FullText updated. Prev: {OriginalValue}. New: {NewValue}",
                    @event.EntityData.Id,
                    fullTextPropUpdatedEvent.OriginalValue,
                    fullTextPropUpdatedEvent.NewValue);
    }
}
