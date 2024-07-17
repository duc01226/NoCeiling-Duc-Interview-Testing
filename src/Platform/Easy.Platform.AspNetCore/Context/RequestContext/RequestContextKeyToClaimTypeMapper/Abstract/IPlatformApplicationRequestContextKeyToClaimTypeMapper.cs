namespace Easy.Platform.AspNetCore.Context.RequestContext.RequestContextKeyToClaimTypeMapper.Abstract;

public interface IPlatformApplicationRequestContextKeyToClaimTypeMapper
{
    public string ToClaimType(string contextKey);
    public HashSet<string> ToOneOfClaimTypes(string contextKey);
}
