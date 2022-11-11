namespace Easy.Platform.Application.Context.UserContext;

public interface IPlatformApplicationUserContext
{
    T GetValue<T>(string contextKey = "");

    void SetValue(object value, string contextKey = "");

    List<string> GetAllKeys();

    Dictionary<string, object> GetAllKeyValues();

    void Clear();
}
