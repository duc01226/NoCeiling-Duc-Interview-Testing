using MongoDB.Bson.Serialization;

namespace Easy.Platform.MongoDB.Serializer.Abstract;

public interface IPlatformMongoBaseSerializer : IBsonSerializer
{
}

public interface IPlatformMongoBaseSerializer<TValue> : IBsonSerializer<TValue>, IPlatformMongoBaseSerializer
{
}

/// <summary>
/// Serializer will be auto register for type
/// </summary>
public interface IPlatformMongoAutoRegisterBaseSerializer<TValue> : IPlatformMongoBaseSerializer<TValue>
{
}
