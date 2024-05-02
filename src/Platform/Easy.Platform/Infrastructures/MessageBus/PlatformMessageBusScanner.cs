using System.Reflection;
using Easy.Platform.Application;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.MessageBus;

public interface IPlatformMessageBusScanner
{
    /// <summary>
    /// Get all type of all defined consumers
    /// </summary>
    List<Type> ScanAllDefinedConsumerTypes();

    /// <summary>
    /// Get all binding routing key of all defined messages
    /// </summary>
    List<string> ScanAllDefinedMessageBindingRoutingKeys();

    /// <summary>
    /// Get all binding routing key of all defined consumers
    /// </summary>
    List<string> ScanAllDefinedConsumerBindingRoutingKeys();

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
            .ConcatSingle(typeof(PlatformApplicationModule).Assembly) // Register built-in consumer type
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessageBusConsumer)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }

    public virtual List<string> ScanAllDefinedMessageBindingRoutingKeys()
    {
        return AllDefaultBindingRoutingKeyForDefinedMessages()
            .Select(p => p.ToString())
            .Distinct()
            .ToList();
    }

    public virtual List<string> ScanAllDefinedConsumerBindingRoutingKeys()
    {
        return AllDefinedMessageBusConsumerAttributes()
            .Select(p => p.ConsumerBindingRoutingKey())
            .Concat(AllDefaultBindingRoutingKeyForDefinedConsumers().Select(p => p.ToString()))
            .Distinct()
            .ToList();
    }

    public virtual List<Assembly> ScanAssemblies()
    {
        return serviceProvider.GetServices<PlatformModule>()
            .Where(p => p is not PlatformInfrastructureModule)
            .SelectMany(p => p.GetServicesRegisterScanAssemblies())
            .Distinct()
            .ToList();
    }

    public virtual List<PlatformConsumerRoutingKeyAttribute> AllDefinedMessageBusConsumerAttributes()
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

    public virtual List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedConsumers()
    {
        return ScanAllDefinedConsumerTypes()
            .Where(messageBusConsumerType => !messageBusConsumerType.GetCustomAttributes<PlatformConsumerRoutingKeyAttribute>().Any())
            .Select(messageBusConsumerType => messageBusConsumerType.FindMatchedGenericType(typeof(IPlatformMessageBusConsumer<>)))
            .Select(IPlatformMessageBusConsumer.BuildForConsumerDefaultBindingRoutingKey)
            .Distinct()
            .ToList();
    }

    public virtual List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForDefinedMessages()
    {
        return AllDefinedMessageTypes()
            .Select(messageType => PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(messageType))
            .Distinct()
            .ToList();
    }

    public virtual List<Type> AllDefinedMessageTypes()
    {
        return ScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsAssignableTo(typeof(IPlatformMessage)) && p.IsClass && !p.IsAbstract)
            .Distinct()
            .ToList();
    }
}
