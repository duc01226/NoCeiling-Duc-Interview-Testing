using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

namespace PlatformExampleApp.TextSnippet.Domain.AssociatedEntities;

/// <summary>
/// This concept is used to demo AssociatedEntity when it's needed for preventing code duplication to handle some logic, return to api, etc...
/// </summary>
public class TextSnippetAssociatedEntity : TextSnippetEntity
{
    public TextSnippetAssociatedEntity(TextSnippetEntity textSnippetEntity, UserEntity createdByUser)
    {
        Util.ValuesCopier.CopyValues(textSnippetEntity, this);
        CreatedByUser = createdByUser;
    }

    /// <summary>
    /// Reference by CreatedByUserId
    /// </summary>
    public UserEntity CreatedByUser { get; set; }

    /// <summary>
    /// Use Builder Pattern by WithXXX because all Associated Props is optional load.
    /// </summary>
    public TextSnippetAssociatedEntity WithCreatedByUser(UserEntity createdByUser)
    {
        CreatedByUser = createdByUser;
        return this;
    }

    public TextSnippetAssociatedEntity WithCreatedByUser(ITextSnippetRepository<UserEntity> userRepo)
    {
        return WithCreatedByUser(
            CreatedByUserId.HasValue ? userRepo.GetByIdAsync(CreatedByUserId.Value).GetResult() : null);
    }
}
