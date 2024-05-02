using Easy.Platform.Domain.Events;
using Easy.Platform.Infrastructures.MessageBus;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Domain.Events;

public class TransferSnippetTextToMultiDbDemoEntityNameDomainEvent : PlatformCqrsDomainEvent, IPlatformSubMessageQueuePrefixSupport
{
    public string SnippetText { get; set; }
    public MultiDbDemoEntity TargetEntity { get; set; }

    public string? SubQueuePrefix()
    {
        return TargetEntity?.Id.ToString();
    }

    public static TransferSnippetTextToMultiDbDemoEntityNameDomainEvent Create(
        string snippetText,
        MultiDbDemoEntity targetEntity)
    {
        return new TransferSnippetTextToMultiDbDemoEntityNameDomainEvent
        {
            SnippetText = snippetText,
            TargetEntity = targetEntity
        };
    }
}
