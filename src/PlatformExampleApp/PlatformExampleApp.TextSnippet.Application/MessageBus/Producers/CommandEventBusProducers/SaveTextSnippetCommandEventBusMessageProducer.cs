using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.UseCaseCommands;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Producers.CommandEventBusProducers;

public class SaveTextSnippetCommandEventBusMessageProducer : PlatformCqrsCommandEventBusMessageProducer<SaveSnippetTextCommand>
{
    public SaveTextSnippetCommandEventBusMessageProducer(
        ILoggerFactory loggerBuilder,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerBuilder,
        unitOfWorkManager,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    /// <summary>
    /// Override this return true if you want to send by MessageSelfRoutingKey. If this true, the consumer need to use <see cref="PlatformConsumerRoutingKeyAttribute"/>
    /// The benefit is that RoutingKey MessageGroup is grouped like PlatformCqrsEntityEvent, PlatformCqrsDomainEvent, PlatformCqrsCommandEvent
    /// </summary>
    /// <returns></returns>
    //protected override bool SendByMessageSelfRoutingKey()
    //{
    //    return true;
    //}
}
