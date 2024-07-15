using System.Security.Claims;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper.Abstract;
using IdentityModel;

namespace Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper;

/// <summary>
/// This will map <see cref="PlatformApplicationCommonRequestContextKeys" /> to <see cref="JwtClaimTypes" />
/// </summary>
public class PlatformApplicationRequestContextKeyToJwtClaimTypeMapper : IPlatformApplicationRequestContextKeyToClaimTypeMapper
{
    public virtual string ToClaimType(string contextKey)
    {
        return contextKey switch
        {
            PlatformApplicationCommonRequestContextKeys.UserIdContextKey => JwtClaimTypes.Subject,
            PlatformApplicationCommonRequestContextKeys.EmailContextKey => JwtClaimTypes.Email,
            PlatformApplicationCommonRequestContextKeys.UserFullNameContextKey => JwtClaimTypes.Name,
            PlatformApplicationCommonRequestContextKeys.UserFirstNameContextKey => JwtClaimTypes.GivenName,
            PlatformApplicationCommonRequestContextKeys.UserMiddleNameContextKey => JwtClaimTypes.MiddleName,
            PlatformApplicationCommonRequestContextKeys.UserLastNameContextKey => JwtClaimTypes.FamilyName,
            PlatformApplicationCommonRequestContextKeys.UserNameContextKey => JwtClaimTypes.PreferredUserName,
            PlatformApplicationCommonRequestContextKeys.UserRolesContextKey => JwtClaimTypes.Role,
            _ => contextKey
        };
    }

    public virtual HashSet<string> ToOneOfClaimTypes(string contextKey)
    {
        return contextKey switch
        {
            PlatformApplicationCommonRequestContextKeys.UserIdContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.NameIdentifier
            ],
            PlatformApplicationCommonRequestContextKeys.UserRolesContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.Role
            ],
            PlatformApplicationCommonRequestContextKeys.EmailContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.Email
            ],
            PlatformApplicationCommonRequestContextKeys.UserFirstNameContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.GivenName
            ],
            PlatformApplicationCommonRequestContextKeys.UserFullNameContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.Name
            ],
            PlatformApplicationCommonRequestContextKeys.UserLastNameContextKey =>
            [
                ToClaimType(contextKey),
                ClaimTypes.Surname
            ],
            _ => [ToClaimType(contextKey)]
        };
    }
}
