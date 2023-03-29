using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common;

public static class PlatformEnvironment
{
    public static string DevelopmentEnvironmentIndicatorText { get; set; } = "Development";

    public static string AspCoreEnvironmentVariableName { get; set; } = "ASPNETCORE_ENVIRONMENT";

    public static string AspCoreEnvironmentValue => Environment.GetEnvironmentVariable(AspCoreEnvironmentVariableName);

    public static bool IsDevelopment => AspCoreEnvironmentValue.ContainsIgnoreCase(DevelopmentEnvironmentIndicatorText);
}
