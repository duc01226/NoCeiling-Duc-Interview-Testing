using Easy.Platform.Common;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsEntityEventApplicationHandler<TEntity> : PlatformCqrsEventApplicationHandler<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
    protected PlatformCqrsEntityEventApplicationHandler(
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
}
