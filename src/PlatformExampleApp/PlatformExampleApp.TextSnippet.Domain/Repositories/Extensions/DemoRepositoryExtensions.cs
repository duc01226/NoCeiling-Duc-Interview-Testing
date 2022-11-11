using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Domain.Repositories.Extensions;

/// <summary>
/// This is for demo that if we want to extend the functionality of generic repository for custom crud logic for an entity
/// </summary>
public static class MultiDbDemoRepositoryExtensions
{
    public static Task<List<MultiDbDemoEntity>> DeleteByName(
        this ITextSnippetRootRepository<MultiDbDemoEntity> repository,
        string name)
    {
        return repository.DeleteManyAsync(p => p.Name == name);
    }
}
