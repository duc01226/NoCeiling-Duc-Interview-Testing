using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Infrastructures.MessageBus;

namespace Easy.Platform.Application.MessageBus;

public class PlatformApplicationMessageBusScanner : PlatformMessageBusScanner
{
    public PlatformApplicationMessageBusScanner(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override List<string> ScanAllDefinedBusMessageAndConsumerBindingRoutingKeys()
    {
        return base.ScanAllDefinedBusMessageAndConsumerBindingRoutingKeys()
            .Concat(AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers().Select(p => p.ToString()))
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers()
    {
        return GetScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsClass && !p.IsAbstract)
            .Select(p => p.FindMatchedGenericType(typeof(PlatformCqrsEventBusMessageProducer<,>)))
            .Where(matchedCqrsEventBusMessageProducerType => matchedCqrsEventBusMessageProducerType != null)
            .Select(
                cqrsEventBusMessageProducerType => PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(
                    messageType: cqrsEventBusMessageProducerType.GetGenericArguments()[1]))
            .Distinct()
            .ToList();
    }
}
