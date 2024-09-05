using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Domain.Events;

public static class SendPlatformCqrsEntityEventExtension
{
    public static async Task SendEntityEvent<TEntity>(
        this IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        if (rootServiceProvider.IsAnyImplementationAssignableToServiceTypeRegistered(typeof(IPlatformCqrsEventHandler<PlatformCqrsEntityEvent<TEntity>>)))
            await cqrs.SendEvent(
                new PlatformCqrsEntityEvent<TEntity>(entity, crudAction).With(x => eventCustomConfig?.Invoke(x)),
                cancellationToken);
    }

    public static async Task SendEntityEvents<TEntity>(
        this IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IList<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        if (rootServiceProvider.IsAnyImplementationAssignableToServiceTypeRegistered(typeof(IPlatformCqrsEventHandler<PlatformCqrsEntityEvent<TEntity>>)))
            await cqrs.SendEvents(
                entities.SelectList(entity => new PlatformCqrsEntityEvent<TEntity>(entity, crudAction).With(x => eventCustomConfig?.Invoke(x))),
                cancellationToken);
    }
}
