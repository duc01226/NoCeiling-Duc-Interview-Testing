namespace Easy.Platform.AspNetCore.Context.RequestContext.UserContextKeyToClaimTypeMapper.Abstract;

public interface IPlatformApplicationRequestContextKeyToClaimTypeMapper
{
    public string ToClaimType(string contextKey);
    public HashSet<string> ToOneOfClaimTypes(string contextKey);
}
