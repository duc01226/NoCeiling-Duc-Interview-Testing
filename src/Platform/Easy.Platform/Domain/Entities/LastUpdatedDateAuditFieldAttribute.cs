using System.Collections.Concurrent;
using System.Reflection;

namespace Easy.Platform.Domain.Entities;

[AttributeUsage(AttributeTargets.Property)]
public class LastUpdatedDateAuditFieldAttribute : Attribute
{
    public static readonly ConcurrentDictionary<Type, PropertyInfo?> CachedAuditFieldByTypeDict = new();

    public static PropertyInfo? GetUpdatedDateAuditField(Type objectType)
    {
        return CachedAuditFieldByTypeDict.GetOrAdd(
            objectType,
            type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.GetCustomAttribute<LastUpdatedDateAuditFieldAttribute>() != null));
    }
}
