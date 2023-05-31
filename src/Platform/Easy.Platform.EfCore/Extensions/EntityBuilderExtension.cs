using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.EfCore.EntityConfiguration.ValueComparers;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Easy.Platform.EfCore.Extensions;

public static class EntityBuilderExtension
{
    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> propertyBuilder) where TProperty : class
    {
        // doNotTryUseRuntimeType = true to Serialize normally not using the runtime type to prevent error.
        // If using runtime type, the ef core entity lazy loading proxies will be the runtime type => lead to error
        return propertyBuilder.HasConversion(
            v => PlatformJsonSerializer.Serialize(v.As<TProperty>()),
            v => PlatformJsonSerializer.Deserialize<TProperty>(v),
            new ToJsonValueComparer<TProperty>());
    }
}
