using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Serializer;

public class PlatformUlidToStringMongoDbSerializer : SerializerBase<Ulid>, IPlatformMongoAutoRegisterBaseSerializer<Ulid>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Ulid value)
    {
        BsonSerializer.Serialize(context.Writer, value.ToString());
    }

    public override Ulid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var ulidValue =
            PlatformNullableUlidToStringMongoDbSerializer.StaticDeserialize(context, args);

        return ulidValue ?? Ulid.Empty;
    }
}

public class PlatformNullableUlidToStringMongoDbSerializer : SerializerBase<Ulid?>, IPlatformMongoAutoRegisterBaseSerializer<Ulid?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Ulid? value)
    {
        BsonSerializer.Serialize(context.Writer, value?.ToString());
    }

    public override Ulid? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return StaticDeserialize(context, args);
    }

    public static Ulid? StaticDeserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return Ulid.Empty;
        }

        return Ulid.Parse(context.Reader.ReadString());
    }
}
