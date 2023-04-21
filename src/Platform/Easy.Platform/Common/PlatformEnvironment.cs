using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common;

public static class PlatformEnvironment
{
    public const string DefaultAspCoreDevelopmentEnvironmentValue = "Development";

    public const string AspCoreEnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";

    public const string AspCoreUrlsVariableName = "ASPNETCORE_URLS";

    public const string DevelopmentEnvironmentIndicatorText = DefaultAspCoreDevelopmentEnvironmentValue;

    public static string? AspCoreEnvironmentValue => Environment.GetEnvironmentVariable(AspCoreEnvironmentVariableName);

    public static string? AspCoreUrlsValue => Environment.GetEnvironmentVariable(AspCoreUrlsVariableName);

    public static bool IsDevelopment => AspCoreEnvironmentValue.ContainsIgnoreCase(DevelopmentEnvironmentIndicatorText);
}
