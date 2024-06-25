using System.Text.Json;
using System.Text.Json.Serialization;

namespace Easy.Platform.Common.JsonSerialization.Converters;

/// <summary>
/// The PlatformPrimitiveTypeToStringJsonConverter is a custom JSON converter that is designed to handle deserialization of JSON primitives into a string. <br />
/// Its primary purpose is to provide a flexible way to convert various JSON primitive types (string, number, boolean) to their string representations when deserializing. <br />
/// This is useful when you have JSON data where the type of some fields might vary (e.g., a field could be a string in some cases, but a number or boolean in others). <br />
/// </summary>
/// <example>
/// The following example demonstrates how to use the PlatformPrimitiveTypeToStringJsonConverter:
/// <code>
/// using System;
/// using System.Text.Json;
/// using System.Text.Json.Serialization;
/// 
/// public class MyClass
/// {
///     public string StringField { get; set; }
///     public string NumberField { get; set; }
///     public string BooleanField { get; set; }
/// }
/// 
/// public class Program
/// {
///     public static void Main()
///     {
///         string json = @"
///         {
///             ""StringField"": ""Hello"",
///             ""NumberField"": 123,
///             ""BooleanField"": true
///         }";
/// 
///         var options = new JsonSerializerOptions();
///         options.Converters.Add(new PlatformPrimitiveTypeToStringJsonConverter());
/// 
///         MyClass obj = JsonSerializer.Deserialize[MyClass](json, options);
/// 
///         Console.WriteLine(obj.StringField);  // Output: Hello
///         Console.WriteLine(obj.NumberField);  // Output: 123
///         Console.WriteLine(obj.BooleanField); // Output: True
///     }
/// }
/// </code>
/// </example>
public class PlatformPrimitiveTypeToStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt32(out var intVal) => intVal.ToString(),
            JsonTokenType.Number when reader.TryGetInt64(out var longVal) => longVal.ToString(),
            JsonTokenType.Number when reader.TryGetDouble(out var doubleVal) => doubleVal.ToString(),
            JsonTokenType.True => "True",
            JsonTokenType.False => "False",
            _ => throw new JsonException() // StartObject, StartArray, Null    
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
