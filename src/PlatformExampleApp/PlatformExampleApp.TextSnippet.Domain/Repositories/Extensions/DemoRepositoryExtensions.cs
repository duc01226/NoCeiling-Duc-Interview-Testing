using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Domain.Repositories.Extensions;

/// <summary>
/// This is for demo that if we want to extend the functionality of generic repository for custom crud logic for an entity
/// </summary>
public static class MultiDbDemoRepositoryExtensions
{
    public static async Task<int> DeleteByName(
        this ITextSnippetRootRepository<MultiDbDemoEntity> repository,
        string name)
    {
        return await repository.DeleteManyAsync(p => p.Name == name);
    }
}
