using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.EfCore.EntityConfiguration.ValueComparers;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;

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

public static class PlatformLoggerConfigurationExtensions
{
    /// <summary>
    /// If you are using Entity Framework with Serilog.Exceptions you must follow the instuctions below,
    /// otherwise in certain cases your entire database will be logged! This is because the exceptions in Entity Framework
    /// have properties that link to the entire database schema in them (See #100, aspnet/EntityFrameworkCore#15214).
    /// Version 8 or newer of Serilog.Exceptions reduces the problem by preventing the destructure of properties that implement
    /// IQueryable but the rest of the DbContext object will still get logged.
    /// </summary>
    public static LoggerConfiguration ApplyPlatformEfCoreSerilogFixMemoryIssues(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.Enrich.WithExceptionDetails(
            new DestructuringOptionsBuilder()
                .WithDefaultDestructurers()
                .WithDestructurers(new[] { new DbUpdateExceptionDestructurer() }));
    }
}
