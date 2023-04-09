using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.UseCaseEvents;

public class DemoUsingPropertyValueUpdatedDomainEventSnippetTextEntityEventHandler : PlatformCqrsEntityEventApplicationHandler<TextSnippetEntity>
{
    public DemoUsingPropertyValueUpdatedDomainEventSnippetTextEntityEventHandler(ILoggerFactory loggerFactory, IUnitOfWorkManager unitOfWorkManager) : base(
        loggerFactory,
        unitOfWorkManager)
    {
    }

    protected override async Task HandleAsync(PlatformCqrsEntityEvent<TextSnippetEntity> @event, CancellationToken cancellationToken)
    {
        // DEMO USING PROPERTY CHANGED DOMAIN EVENT
        var snippetTextPropUpdatedEvent = @event.FindPropertyValueUpdatedDomainEvent(p => p.SnippetText);
        if (snippetTextPropUpdatedEvent != null)
            Logger.LogInformation(
                $"TextSnippetEntity Id:'{@event.EntityData.Id}' SnippetText updated. Prev: {snippetTextPropUpdatedEvent.OriginalValue}. New: {snippetTextPropUpdatedEvent.NewValue}");

        var fullTextPropUpdatedEvent = @event.FindPropertyValueUpdatedDomainEvent(p => p.FullText);
        if (fullTextPropUpdatedEvent != null)
            Logger.LogInformation(
                $"TextSnippetEntity Id:'{@event.EntityData.Id}' FullText updated. Prev: {fullTextPropUpdatedEvent.OriginalValue}. New: {fullTextPropUpdatedEvent.NewValue}");
    }
}
