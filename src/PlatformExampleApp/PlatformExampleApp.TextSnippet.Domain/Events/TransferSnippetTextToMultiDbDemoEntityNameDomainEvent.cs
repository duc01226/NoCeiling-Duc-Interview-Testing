using Easy.Platform.Domain.Events;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Domain.Events;

public class TransferSnippetTextToMultiDbDemoEntityNameDomainEvent : PlatformCqrsDomainEvent
{
    public string SnippetText { get; set; }
    public MultiDbDemoEntity TargetEntity { get; set; }

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
