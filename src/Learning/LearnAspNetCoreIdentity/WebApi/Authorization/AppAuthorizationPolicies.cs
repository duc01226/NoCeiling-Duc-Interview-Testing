namespace WebApi.Authorization;

public static class AppAuthorizationPolicies
{
    public const string MustBelongToHrDepartment = "MustBelongToHrDepartment";
    public const string AdminOnly = "AdminOnly";
    public const string HrManagerOnly = "HrManagerOnly";
}
