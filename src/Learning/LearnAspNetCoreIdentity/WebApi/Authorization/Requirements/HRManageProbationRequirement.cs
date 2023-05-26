using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WebApi.Auth;
using WebApi.Common.Extensions;

namespace WebApi.Authorization.Requirements;

public class HrManageProbationRequirement : IAuthorizationRequirement
{
    public const int DefaultMinimumProbationMonths = 3;

    public HrManageProbationRequirement(int probationMonths)
    {
        ProbationMonths = probationMonths;
    }

    public int ProbationMonths { get; set; }

    public int ProbationDays => ProbationMonths * 30;
}
