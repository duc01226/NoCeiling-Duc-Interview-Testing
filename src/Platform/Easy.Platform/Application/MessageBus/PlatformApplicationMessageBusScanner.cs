using System.Reflection;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Infrastructures.MessageBus;

namespace Easy.Platform.Application.MessageBus;

public sealed class PlatformApplicationMessageBusScanner : PlatformMessageBusScanner
{
    public PlatformApplicationMessageBusScanner(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override List<string> ScanAllDefinedMessageBindingRoutingKeys()
    {
        return base.ScanAllDefinedMessageBindingRoutingKeys()
            .Concat(AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers().Select(p => p.ToString()))
            .ToList();
    }

    public override List<Assembly> ScanAssemblies()
    {
        return base.ScanAssemblies()
            .ConcatSingle(typeof(PlatformApplicationModule).Assembly)
            .Distinct()
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers()
    {
        return ScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsClass && !p.IsAbstract)
            .Select(p => p.FindMatchedGenericType(typeof(PlatformCqrsEventBusMessageProducer<,>)))
            .Where(matchedCqrsEventBusMessageProducerType => matchedCqrsEventBusMessageProducerType != null)
            .Select(
                cqrsEventBusMessageProducerType => PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(
                    messageType: IPlatformCqrsEventBusMessageProducer.GetTMessageArgumentType(cqrsEventBusMessageProducerType)))
            .Distinct()
            .ToList();
    }
}
