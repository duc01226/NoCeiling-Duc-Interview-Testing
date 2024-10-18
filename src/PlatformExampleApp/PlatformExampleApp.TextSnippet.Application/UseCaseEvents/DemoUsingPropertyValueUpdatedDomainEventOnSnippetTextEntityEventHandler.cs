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
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }

    // Default is true to improve performance when command save, the event is executed separately and could be in parallel.
    // Set it to false if you want the event executed sync with the command and in order
    // protected override bool AllowHandleInBackgroundThread() => false;

    // Can override to return False to TURN OFF support for store cqrs event handler as inbox
    // protected override bool EnableHandleEventFromInboxBusMessage => false;

    public override async Task<bool> HandleWhen(PlatformCqrsEntityEvent<TextSnippetEntity> @event)
    {
        return true;
    }

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

        if (@event.HasAnyFieldUpdatedEvents(p => p.Address, p => p.AddressStrings, p => p.Addresses) &&
            SomeCustomBuildMessageResult(@event.ExistingOriginalEntityData).IsValuesDifferent(SomeCustomBuildMessageResult(@event.EntityData)))
            CreateGlobalLogger()
                .LogInformation(
                    "TextSnippetEntity Id:'{Id}' FullText Address Info updated",
                    @event.EntityData.Id);
    }

    private static object SomeCustomBuildMessageResult(TextSnippetEntity entityData)
    {
        return new { entityData.Address, entityData.AddressStrings, entityData.Addresses };
    }
}
