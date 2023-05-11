using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsCommandEventBusMessageConsumer<TCommand> : IPlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}

public abstract class PlatformCqrsCommandEventBusMessageConsumer<TCommand>
    : PlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>>, IPlatformCqrsCommandEventBusMessageConsumer<TCommand>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventBusMessageConsumer(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerBuilder, uowManager, serviceProvider)
    {
    }
}
