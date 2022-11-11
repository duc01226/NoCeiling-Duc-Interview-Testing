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
    List<Type> ScanAllDefinedMessageBusConsumerTypes();

    /// <summary>
    /// Get all binding routing key of all defined message and consumers
    /// </summary>
    List<string> ScanAllDefinedBusMessageAndConsumerBindingRoutingKeys();

    /// <summary>
    /// Get all assemblies for scanning event bus message/consumer
    /// </summary>
    List<Assembly> GetScanAssemblies();
}

public class PlatformMessageBusScanner : IPlatformMessageBusScanner
{
    private readonly IServiceProvider serviceProvider;

    public PlatformMessageBusScanner(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public virtual List<Type> ScanAllDefinedMessageBusConsumerTypes()
    {
        return GetScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessageBusConsumer)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }

    public virtual List<string> ScanAllDefinedBusMessageAndConsumerBindingRoutingKeys()
    {
        return AllDefinedConsumerAttributeBindingRoutingKeys()
            .Concat(AllDefaultBindingRoutingKeyForDefinedConsumers().Select(p => p.ToString()))
            .Concat(AllDefaultBindingRoutingKeyForDefinedBusMessages().Select(p => p.ToString()))
            .Distinct()
            .ToList();
    }

    public List<Assembly> GetScanAssemblies()
    {
        return serviceProvider.GetServices<PlatformModule>()
            .Where(p => !p.GetType().IsAssignableTo(typeof(PlatformInfrastructureModule)))
            .Select(p => p.Assembly)
            .ToList();
    }

    public List<PlatformConsumerRoutingKeyAttribute> AllDefinedMessageBusConsumerAttributes()
    {
        return ScanAllDefinedMessageBusConsumerTypes()
            .SelectMany(
                messageConsumerType => messageConsumerType
                    .GetCustomAttributes(true)
                    .OfType<PlatformConsumerRoutingKeyAttribute>()
                    .Select(
                        messageConsumerTypeAttribute => new
                        {
                            MessageConsumerTypeAttribute = messageConsumerTypeAttribute,
                            ConsumerBindingRoutingKey = messageConsumerTypeAttribute.GetConsumerBindingRoutingKey()
                        }))
            .GroupBy(p => p.ConsumerBindingRoutingKey, p => p.MessageConsumerTypeAttribute)
            .Select(group => group.First())
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedConsumers()
    {
        return ScanAllDefinedMessageBusConsumerTypes()
            .Where(messageBusConsumerType => !messageBusConsumerType.GetCustomAttributes<PlatformConsumerRoutingKeyAttribute>().Any())
            .Select(messageBusConsumerType => messageBusConsumerType.FindMatchedGenericType(typeof(IPlatformMessageBusConsumer<>)))
            .Select(consumerGenericType => IPlatformMessageBusConsumer.BuildForConsumerDefaultBindingRoutingKey(consumerGenericType))
            .Distinct()
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedBusMessages()
    {
        return AllDefinedBusBusMessageTypes()
            .Select(messageType => PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(messageType))
            .Distinct()
            .ToList();
    }

    public List<string> AllDefinedConsumerAttributeBindingRoutingKeys()
    {
        return AllDefinedMessageBusConsumerAttributes().SelectList(p => p.GetConsumerBindingRoutingKey());
    }

    public List<Type> AllDefinedBusBusMessageTypes()
    {
        return GetScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessage)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }
}
