using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Application.MessageBus.Producers.EntityEventBusProducers;

public class TextSnippetEntityEventBusMessageProducer
    : PlatformCqrsEntityEventBusMessageProducer<TextSnippetEntityEventBusMessage, TextSnippetEntity>
{
    public TextSnippetEntityEventBusMessageProducer(
        ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(
        loggerFactory,
        unitOfWorkManager,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    // Can override this to set more value for message if you have extra props
    //protected override TextSnippetEntityEventBusMessage BuildMessage(PlatformCqrsEntityEvent<TextSnippetEntity> @event)
    //{
    //    return base.BuildMessage(@event)
    //        .With(_ => _.CustomAdditionalProp = "CustomAdditionalPropValue");
    //}


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

public class TextSnippetEntityEventBusMessage : PlatformCqrsEntityEventBusMessage<TextSnippetEntity>
{
    // public string CustomAdditionalProp { get; set; }
}
