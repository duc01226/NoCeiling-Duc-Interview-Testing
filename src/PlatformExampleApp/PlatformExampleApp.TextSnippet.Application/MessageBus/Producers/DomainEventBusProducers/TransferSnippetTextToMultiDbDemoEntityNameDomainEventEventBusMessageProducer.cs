using Easy.Platform.Application;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Events;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Producers.DomainEventBusProducers;

public class TransferSnippetTextToMultiDbDemoEntityNameDomainEventSendWithDefaultRoutingKeyEventBusMessageProducer
    : PlatformCqrsDomainEventBusMessageProducer<TransferSnippetTextToMultiDbDemoEntityNameDomainEvent>
{
    public TransferSnippetTextToMultiDbDemoEntityNameDomainEventSendWithDefaultRoutingKeyEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationRequestContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    /// <summary>
    /// Override this return true if you want to send by MessageSelfRoutingKey. If this true, the consumer need to use <see cref="PlatformConsumerRoutingKeyAttribute" />
    /// The benefit is that RoutingKey MessageGroup is grouped like PlatformCqrsEntityEvent, PlatformCqrsDomainEvent, PlatformCqrsCommandEvent
    /// </summary>
    /// <returns></returns>
    //protected override bool SendByMessageSelfRoutingKey()
    //{
    //    return true;
    //}
}
