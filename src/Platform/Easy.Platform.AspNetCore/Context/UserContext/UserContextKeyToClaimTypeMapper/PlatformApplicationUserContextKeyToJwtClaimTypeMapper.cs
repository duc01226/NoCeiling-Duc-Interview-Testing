using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;
using IdentityModel;

namespace Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper;

/// <summary>
/// This will map <see cref="PlatformApplicationCommonUserContextKeys"/> to <see cref="JwtClaimTypes"/>
/// </summary>
public class PlatformApplicationUserContextKeyToJwtClaimTypeMapper : IPlatformApplicationUserContextKeyToClaimTypeMapper
{
    public virtual string ToClaimType(string contextKey)
    {
        return contextKey switch
        {
            PlatformApplicationCommonUserContextKeys.UserIdContextKey => JwtClaimTypes.Subject,
            PlatformApplicationCommonUserContextKeys.EmailContextKey => JwtClaimTypes.Email,
            PlatformApplicationCommonUserContextKeys.UserFullNameContextKey => JwtClaimTypes.Name,
            PlatformApplicationCommonUserContextKeys.UserFirstNameContextKey => JwtClaimTypes.GivenName,
            PlatformApplicationCommonUserContextKeys.UserMiddleNameContextKey => JwtClaimTypes.MiddleName,
            PlatformApplicationCommonUserContextKeys.UserLastNameContextKey => JwtClaimTypes.FamilyName,
            PlatformApplicationCommonUserContextKeys.UserNameContextKey => JwtClaimTypes.PreferredUserName,
            PlatformApplicationCommonUserContextKeys.UserRolesContextKey => JwtClaimTypes.Role,
            _ => contextKey
        };
    }

    public virtual HashSet<string> ToOneOfClaimTypes(string contextKey)
    {
        return new HashSet<string>
        {
            ToClaimType(contextKey)
        };
    }
}
