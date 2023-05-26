using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Serializer;

public sealed class PlatformTimeOnlyToStringMongoDbSerializer : SerializerBase<TimeOnly>, IPlatformMongoBaseSerializer<TimeOnly>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TimeOnly value)
    {
        BsonSerializer.Serialize(context.Writer, value.ToString());
    }

    public override TimeOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return new TimeOnly(0, 0, 0);
        }

        return TimeOnly.Parse(context.Reader.ReadString());
    }
}

public sealed class PlatformNullableTimeOnlyToStringMongoDbSerializer : SerializerBase<TimeOnly?>, IPlatformMongoBaseSerializer<TimeOnly?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TimeOnly? value)
    {
        BsonSerializer.Serialize(context.Writer, value?.ToString());
    }

    public override TimeOnly? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }

        return TimeOnly.Parse(context.Reader.ReadString());
    }
}
