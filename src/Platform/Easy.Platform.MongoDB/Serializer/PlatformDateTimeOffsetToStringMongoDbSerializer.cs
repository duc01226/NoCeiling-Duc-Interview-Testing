using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Serializer;

public class PlatformDateTimeOffsetToStringMongoDbSerializer
    : SerializerBase<DateTimeOffset>, IPlatformMongoBaseSerializer<DateTimeOffset>, IPlatformMongoAutoRegisterBaseSerializer<DateTimeOffset>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTimeOffset value)
    {
        BsonSerializer.Serialize(context.Writer, value.ToString());
    }

    public override DateTimeOffset Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return new DateTimeOffset(0, 0, 0, 0, 0, 0, TimeSpan.Zero);
        }

        return DateTimeOffset.Parse(context.Reader.ReadString());
    }
}

public class PlatformNullableDateTimeOffsetToStringMongoDbSerializer
    : SerializerBase<DateTimeOffset?>, IPlatformMongoBaseSerializer<DateTimeOffset?>, IPlatformMongoAutoRegisterBaseSerializer<DateTimeOffset?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTimeOffset? value)
    {
        BsonSerializer.Serialize(context.Writer, value?.ToString());
    }

    public override DateTimeOffset? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }

        return DateTimeOffset.Parse(context.Reader.ReadString());
    }
}
