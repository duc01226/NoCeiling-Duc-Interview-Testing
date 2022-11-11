using System.Drawing;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace PlatformExampleApp.TextSnippet.Persistence.Mongo.Serializers;

/// <summary>
/// Example implement a custom serializer for any data type.
/// This will be registered automatically via <see cref="IPlatformMongoAutoRegisterBaseSerializer{TValue}"/>
/// </summary>
public class ExampleColorSerializer : StructSerializerBase<Color>, IPlatformMongoAutoRegisterBaseSerializer<Color>
{
    public override Color Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return PlatformJsonSerializer.Deserialize<Color>(context.Reader.ReadString());
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Color value)
    {
        context.Writer.WriteString(PlatformJsonSerializer.Serialize(value));
    }
}
