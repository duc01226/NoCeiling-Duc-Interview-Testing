namespace Easy.Platform.Common;

public static class PlatformEnvironment
{
    public static string AspCoreEnvVariableName { get; set; } = "ASPNETCORE_ENVIRONMENT";

    public static string Name => Environment.GetEnvironmentVariable(AspCoreEnvVariableName);

    public static bool IsDevelopment => Name.Contains("Development");
}
