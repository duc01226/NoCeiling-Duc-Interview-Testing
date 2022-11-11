namespace WebApi.Auth;

public static class AppAuthenticationClaims
{
    public static class HrDepartment
    {
        public const string Type = "Department";

        public const string Value = "HR";
    }

    public static class Admin
    {
        public const string Type = "Admin";

        public const string Value = "True";
    }

    public static class Manager
    {
        public const string Type = "Manager";

        public const string Value = "True";
    }

    public static class EmploymentDate
    {
        public const string Type = "EmploymentDate";
    }
}
