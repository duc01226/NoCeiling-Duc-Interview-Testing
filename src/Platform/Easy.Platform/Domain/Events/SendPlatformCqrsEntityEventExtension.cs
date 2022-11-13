using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Domain.Events;

public static class SendPlatformCqrsEntityEventExtension
{
    public static async Task SendEntityEvent<TEntity>(
        this IPlatformCqrs cqrs,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        await cqrs.SendEvent(
            new PlatformCqrsEntityEvent<TEntity>(entity, crudAction),
            cancellationToken);
    }

    public static async Task SendEntityEvents<TEntity>(
        this IPlatformCqrs cqrs,
        IList<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        await cqrs.SendEvents(
            entities.SelectList(entity => new PlatformCqrsEntityEvent<TEntity>(entity, crudAction)),
            cancellationToken);
    }
}
