namespace Easy.Platform.AspNetCore.Context.UserContext.UserContextKeyToClaimTypeMapper.Abstract;

public interface IPlatformApplicationRequestContextKeyToClaimTypeMapper
{
    public string ToClaimType(string contextKey);
    public HashSet<string> ToOneOfClaimTypes(string contextKey);
}
