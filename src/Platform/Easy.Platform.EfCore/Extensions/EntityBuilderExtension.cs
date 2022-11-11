using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.EfCore.EntityConfiguration.ValueComparers;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Easy.Platform.EfCore.Extensions;

public static class EntityBuilderExtension
{
    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        return propertyBuilder.HasConversion(
            v => PlatformJsonSerializer.Serialize(v),
            v => PlatformJsonSerializer.Deserialize<TProperty>(v),
            new ToJsonValueComparer<TProperty>());
    }
}
