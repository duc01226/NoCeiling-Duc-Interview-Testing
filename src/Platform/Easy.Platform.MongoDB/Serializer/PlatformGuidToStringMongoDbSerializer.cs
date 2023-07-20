using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Serializer;

public class PlatformGuidToStringMongoDbSerializer : SerializerBase<Guid>, IPlatformMongoBaseSerializer<Guid>
{
    private readonly GuidSerializer guidAsBinarySerializer = new(BsonType.Binary);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
    {
        BsonSerializer.Serialize(context.Writer, value.ToString());
    }

    public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var guidValue =
            PlatformNullableGuidToStringMongoDbSerializer.Deserialize(guidAsBinarySerializer, context, args);

        return guidValue ?? Guid.Empty;
    }
}

public class PlatformNullableGuidToStringMongoDbSerializer : SerializerBase<Guid?>, IPlatformMongoBaseSerializer<Guid?>
{
    private readonly GuidSerializer guidAsBinarySerializer = new(BsonType.Binary);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid? value)
    {
        BsonSerializer.Serialize(context.Writer, value?.ToString());
    }

    public override Guid? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return Deserialize(guidAsBinarySerializer, context, args);
    }

    public static Guid? Deserialize(
        GuidSerializer guidAsBinarySerializer,
        BsonDeserializationContext context,
        BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Binary)
            return guidAsBinarySerializer.Deserialize(context, args);

        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return Guid.Empty;
        }

        return Guid.Parse(context.Reader.ReadString());
    }
}
