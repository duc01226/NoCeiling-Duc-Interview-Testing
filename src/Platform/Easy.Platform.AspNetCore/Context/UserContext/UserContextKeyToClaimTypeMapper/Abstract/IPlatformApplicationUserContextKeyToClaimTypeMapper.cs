namespace Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;

public interface IPlatformApplicationUserContextKeyToClaimTypeMapper
{
    public string ToClaimType(string contextKey);
    public HashSet<string> ToOneOfClaimTypes(string contextKey);
}
