using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;

namespace Easy.Platform.EfCore.Logging.Exceptions;

public static class PlatformLoggerConfigurationExtensions
{
    /// <summary>
    /// If you are using Entity Framework with Serilog.Exceptions you must follow the instructions below,
    /// otherwise in certain cases your entire database will be logged! This is because the exceptions in Entity Framework
    /// have properties that link to the entire database schema in them (See #100, aspnet/EntityFrameworkCore#15214).
    /// Version 8 or newer of Serilog.Exceptions reduces the problem by preventing the destructure of properties that implement
    /// IQueryable but the rest of the DbContext object will still get logged.
    /// </summary>
    public static DestructuringOptionsBuilder WithPlatformEfCoreExceptionDetailsDestructurers(this DestructuringOptionsBuilder destructuringOptionsBuilder)
    {
        return destructuringOptionsBuilder
            .WithPlatformDbUpdateExceptionDestructurer()
            .WithSqlServerExceptionDestructurer();
    }

    public static DestructuringOptionsBuilder WithPlatformDbUpdateExceptionDestructurer(this DestructuringOptionsBuilder destructuringOptionsBuilder)
    {
        return destructuringOptionsBuilder.WithDestructurersIfNotExist(new PlatformEfCoreDbUpdateExceptionDestructurer());
    }

    /// <summary>
    /// To avoid the reflection based destructurer for SqlException when using System.Data.SqlClient. <br />
    /// https://github.com/RehanSaeed/Serilog.Exceptions#Serilog.Exceptions.EntityFrameworkCore
    /// </summary>
    public static DestructuringOptionsBuilder WithSqlServerExceptionDestructurer(this DestructuringOptionsBuilder destructuringOptionsBuilder)
    {
        return destructuringOptionsBuilder
            .WithDestructurersIfNotExist(new PlatformMicrosoftSqlExceptionDestructurer())
            .WithDestructurersIfNotExist(new PlatformSystemSqlExceptionDestructurer());
    }

    public static DestructuringOptionsBuilder WithDestructurersIfNotExist<TExceptionDestructurer>(
        this DestructuringOptionsBuilder destructuringOptionsBuilder,
        TExceptionDestructurer destructurer) where TExceptionDestructurer : ExceptionDestructurer
    {
        if (!destructuringOptionsBuilder.Destructurers.Any(p => p is TExceptionDestructurer))
            destructuringOptionsBuilder.WithDestructurers([destructurer]);

        return destructuringOptionsBuilder;
    }
}
