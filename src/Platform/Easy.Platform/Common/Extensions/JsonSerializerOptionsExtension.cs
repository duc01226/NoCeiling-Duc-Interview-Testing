using System.Text.Json;

namespace Easy.Platform.Common.Extensions;

public static class JsonSerializerOptionsExtension
{
    public static JsonSerializerOptions Clone(this JsonSerializerOptions options)
    {
        var cloned = new JsonSerializerOptions();

        typeof(JsonSerializerOptions)
            .GetProperties()
            .Where(p => p.CanWrite)
            .ToList()
            .ForEach(
                p =>
                {
                    p.SetValue(cloned, p.GetValue(options));
                });

        options.Converters.ForEach(cloned.Converters.Add);

        return cloned;
    }
}
