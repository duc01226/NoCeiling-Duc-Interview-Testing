using Easy.Platform.Common.JsonSerialization.Converters;
using Easy.Platform.MongoDB.Serializer.Abstract;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Easy.Platform.MongoDB.Serializer;

public class PlatformDynamicObjectMongoDbSerializer : SerializerBase<object>, IPlatformMongoAutoRegisterBaseSerializer<object>
{
    public override object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return TryGetReflectionDynamic(BsonSerializer.Deserialize<BsonValue>(context.Reader));
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        if (value == null)
            context.Writer.WriteNull();
        else
            BsonSerializer.Serialize(
                context.Writer,
                PlatformObjectJsonConverter.TryGetReflectionDynamicIfJsonElement(value));
    }

    private static dynamic TryGetReflectionDynamic(BsonValue dynamicObjectAsBsonValue)
    {
        switch (dynamicObjectAsBsonValue.BsonType)
        {
            case BsonType.Int32:
                return dynamicObjectAsBsonValue.AsInt32;
            case BsonType.Int64:
                return dynamicObjectAsBsonValue.AsInt64;
            case BsonType.Double:
                return dynamicObjectAsBsonValue.AsDouble;
            case BsonType.Decimal128:
                return dynamicObjectAsBsonValue.AsDecimal128;
            case BsonType.DateTime:
                return dynamicObjectAsBsonValue.ToUniversalTime();
            case BsonType.Boolean:
                return dynamicObjectAsBsonValue.AsBoolean;
            case BsonType.String:
            {
                if (DateTimeOffset.TryParse(dynamicObjectAsBsonValue.AsString, out var dateTimeOffsetValue))
                    return dateTimeOffsetValue;
                if (DateTime.TryParse(dynamicObjectAsBsonValue.AsString, out var dateValue))
                    return dateValue;
                return dynamicObjectAsBsonValue.AsString;
            }
            case BsonType.Array:
                return dynamicObjectAsBsonValue
                    .AsBsonArray
                    .Select(o => TryGetReflectionDynamic(o))
                    .ToArray();
            case BsonType.Document:
                return dynamicObjectAsBsonValue.AsBsonDocument.ToDictionary(item => item.Name, item => item.Value);
            case BsonType.Undefined:
            case BsonType.Null:
            default:
                return null;
        }
    }
}
