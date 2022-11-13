using System.Reflection;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusScanner
{
    /// <summary>
    /// Get all routing key pattern of all defined consumers
    /// </summary>
    List<Type> ScanAllDefinedConsumerTypes();

    /// <summary>
    /// Get all binding routing key of all defined message and consumers
    /// </summary>
    List<string> ScanAllDefinedMessageAndConsumerBindingRoutingKeys();

    /// <summary>
    /// Get all assemblies for scanning event bus message/consumer
    /// </summary>
    List<Assembly> ScanAssemblies();
}

public class PlatformMessageBusScanner : IPlatformMessageBusScanner
{
    private readonly IServiceProvider serviceProvider;

    public PlatformMessageBusScanner(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public virtual List<Type> ScanAllDefinedConsumerTypes()
    {
        return ScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessageBusConsumer)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }

    public virtual List<string> ScanAllDefinedMessageAndConsumerBindingRoutingKeys()
    {
        return AllDefinedMessageBusConsumerAttributes().Select(p => p.ConsumerBindingRoutingKey())
            .Concat(AllDefaultBindingRoutingKeyForDefinedConsumers().Select(p => p.ToString()))
            .Concat(AllDefaultBindingRoutingKeyForDefinedMessages().Select(p => p.ToString()))
            .Distinct()
            .ToList();
    }

    public List<Assembly> ScanAssemblies()
    {
        return serviceProvider.GetServices<PlatformModule>()
            .Where(p => p is not PlatformInfrastructureModule)
            .Select(p => p.Assembly)
            .ToList();
    }

    public List<PlatformConsumerRoutingKeyAttribute> AllDefinedMessageBusConsumerAttributes()
    {
        return ScanAllDefinedConsumerTypes()
            .SelectMany(
                messageConsumerType => messageConsumerType
                    .GetCustomAttributes(true)
                    .OfType<PlatformConsumerRoutingKeyAttribute>()
                    .Select(
                        messageConsumerTypeAttribute => new
                        {
                            MessageConsumerTypeAttribute = messageConsumerTypeAttribute,
                            ConsumerBindingRoutingKey = messageConsumerTypeAttribute.ConsumerBindingRoutingKey()
                        }))
            .GroupBy(p => p.ConsumerBindingRoutingKey, p => p.MessageConsumerTypeAttribute)
            .Select(group => group.First())
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedConsumers()
    {
        return ScanAllDefinedConsumerTypes()
            .Where(messageBusConsumerType => !messageBusConsumerType.GetCustomAttributes<PlatformConsumerRoutingKeyAttribute>().Any())
            .Select(messageBusConsumerType => messageBusConsumerType.FindMatchedGenericType(typeof(IPlatformMessageBusConsumer<>)))
            .Select(IPlatformMessageBusConsumer.BuildForConsumerDefaultBindingRoutingKey)
            .Distinct()
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedMessages()
    {
        return AllDefinedMessageTypes()
            .Select(messageType => PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(messageType))
            .Distinct()
            .ToList();
    }

    public List<Type> AllDefinedMessageTypes()
    {
        return ScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessage)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }
}
