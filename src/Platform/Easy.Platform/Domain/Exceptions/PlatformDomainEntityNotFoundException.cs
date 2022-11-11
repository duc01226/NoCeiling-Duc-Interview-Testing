using Easy.Platform.Common.Exceptions;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Domain.Exceptions;

public class PlatformDomainEntityNotFoundException<TEntity> : PlatformNotFoundException where TEntity : IEntity
{
    public PlatformDomainEntityNotFoundException(string entityId, Exception innerException = null) : base(
        BuildErrorMsg(entityId),
        innerException)
    {
        EntityId = entityId;
    }

    public string EntityId { get; set; }

    public static string BuildErrorMsg(string entityId, string errorMsg = null)
    {
        return errorMsg ?? $"{typeof(TEntity).Name} with Id:{entityId} is not found";
    }
}
