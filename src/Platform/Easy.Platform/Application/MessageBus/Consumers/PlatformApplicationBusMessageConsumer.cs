using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.MessageBus.Consumers;

public interface IPlatformApplicationMessageBusConsumer<in TMessage> : IPlatformMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
}

public abstract class PlatformApplicationMessageBusConsumer<TMessage> : PlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    protected readonly IPlatformInboxBusMessageRepository InboxBusMessageRepo;
    protected readonly PlatformInboxConfig InboxConfig;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UowManager;

    protected PlatformApplicationMessageBusConsumer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider) : base(loggerFactory)
    {
        UowManager = uowManager;
        InboxBusMessageRepo = serviceProvider.GetService<IPlatformInboxBusMessageRepository>();
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        ServiceProvider = serviceProvider;
    }

    public virtual bool AutoBeginUow => true;

    public override async Task HandleAsync(TMessage message, string routingKey)
    {
        try
        {
            if (AutoBeginUow)
                using (var uow = UowManager.Begin())
                {
                    await ExecuteInternalHandleAsync(message, routingKey);
                    await uow.CompleteAsync();
                }
            else
                await ExecuteInternalHandleAsync(message, routingKey);
        }
        catch (Exception e)
        {
            Logger.LogError(
                e,
                $"[{GetType().FullName}] Error Consume message [RoutingKey:{{RoutingKey}}], [Type:{{BusMessage_Type}}].{Environment.NewLine}" +
                $"Message Info: {{BusMessage}}.{Environment.NewLine}",
                routingKey,
                message.GetType().GetNameOrGenericTypeName(),
                message.ToJson());
            throw;
        }
    }

    protected virtual async Task ExecuteInternalHandleAsync(TMessage message, string routingKey)
    {
        if (InboxBusMessageRepo != null)
            await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerInternalHandleAsync(
                ServiceProvider,
                consumer: this,
                InboxBusMessageRepo,
                InternalHandleAsync,
                message,
                routingKey,
                Logger,
                InboxConfig.RetryProcessFailedMessageInSecondsUnit,
                HandleExistingInboxMessageTrackId);
        else
            await InternalHandleAsync(message, routingKey);
    }
}
