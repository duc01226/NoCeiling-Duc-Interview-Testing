using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Dynamic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

#pragma warning disable S907

namespace Easy.Platform.MongoDB.Serializer;

/// <summary>
/// Represents a serializer for objects.
/// Copy code from and Replace default <see cref="ObjectSerializer" /> to handle auto smart string in date format to Date
/// </summary>
public sealed class PlatformDynamicObjectMongoDbSerializer : ClassSerializerBase<object>, IHasDiscriminatorConvention
{
    // private fields
    private readonly GuidSerializer guidSerializer;

    // constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    public PlatformDynamicObjectMongoDbSerializer()
        : this(BsonSerializer.LookupDiscriminatorConvention(typeof(object)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <exception cref="System.ArgumentNullException">discriminatorConvention</exception>
    public PlatformDynamicObjectMongoDbSerializer(IDiscriminatorConvention discriminatorConvention)
        : this(discriminatorConvention, GuidRepresentation.Unspecified)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <param name="guidRepresentation">The Guid representation.</param>
    public PlatformDynamicObjectMongoDbSerializer(IDiscriminatorConvention discriminatorConvention, GuidRepresentation guidRepresentation)
        : this(discriminatorConvention, guidRepresentation, DefaultFrameworkAllowedTypes.AllowedTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="allowedTypes">A delegate that determines what types are allowed.</param>
    public PlatformDynamicObjectMongoDbSerializer(Func<Type, bool> allowedTypes)
        : this(BsonSerializer.LookupDiscriminatorConvention(typeof(object)), allowedTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <param name="allowedTypes">A delegate that determines what types are allowed.</param>
    public PlatformDynamicObjectMongoDbSerializer(IDiscriminatorConvention discriminatorConvention, Func<Type, bool> allowedTypes)
        : this(discriminatorConvention, GuidRepresentation.Unspecified, allowedTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <param name="guidRepresentation">The Guid representation.</param>
    /// <param name="allowedTypes">A delegate that determines what types are allowed.</param>
    public PlatformDynamicObjectMongoDbSerializer(IDiscriminatorConvention discriminatorConvention, GuidRepresentation guidRepresentation, Func<Type, bool> allowedTypes)
        : this(discriminatorConvention, guidRepresentation, allowedTypes ?? throw new ArgumentNullException(nameof(allowedTypes)), allowedTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDynamicObjectMongoDbSerializer" /> class.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <param name="guidRepresentation">The Guid representation.</param>
    /// <param name="allowedDeserializationTypes">A delegate that determines what types are allowed to be deserialized.</param>
    /// <param name="allowedSerializationTypes">A delegate that determines what types are allowed to be serialized.</param>
    public PlatformDynamicObjectMongoDbSerializer(
        IDiscriminatorConvention discriminatorConvention,
        GuidRepresentation guidRepresentation,
        Func<Type, bool> allowedDeserializationTypes,
        Func<Type, bool> allowedSerializationTypes)
    {
        DiscriminatorConvention = discriminatorConvention ?? throw new ArgumentNullException(nameof(discriminatorConvention));
        GuidRepresentation = guidRepresentation;
        guidSerializer = new GuidSerializer(GuidRepresentation);
        AllowedDeserializationTypes = allowedDeserializationTypes ?? throw new ArgumentNullException(nameof(allowedDeserializationTypes));
        AllowedSerializationTypes = allowedSerializationTypes ?? throw new ArgumentNullException(nameof(allowedSerializationTypes));
    }

    // public properties
    /// <summary>
    /// Gets the AllowedDeserializationTypes filter;
    /// </summary>
    public Func<Type, bool> AllowedDeserializationTypes { get; }

    /// <summary>
    /// Gets the AllowedSerializationTypes filter;
    /// </summary>
    public Func<Type, bool> AllowedSerializationTypes { get; }

    /// <summary>
    /// Gets the GuidRepresentation.
    /// </summary>
    public GuidRepresentation GuidRepresentation { get; }

    /// <summary>
    /// Gets the discriminator convention.
    /// </summary>
    public IDiscriminatorConvention DiscriminatorConvention { get; }

    // public methods
    /// <summary>
    /// Deserializes a value.
    /// </summary>
    /// <param name="context">The deserialization context.</param>
    /// <param name="args">The deserialization args.</param>
    /// <returns>A deserialized value.</returns>
    public override object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonReader = context.Reader;

        var bsonType = bsonReader.GetCurrentBsonType();
        switch (bsonType)
        {
            case BsonType.Array:
                if (context.DynamicArraySerializer != null) return context.DynamicArraySerializer.Deserialize(context);
                goto default;

            case BsonType.Binary:
                var binaryDataBookmark = bsonReader.GetBookmark();
                var binaryData = bsonReader.ReadBinaryData();
                var subType = binaryData.SubType;
                if (subType == BsonBinarySubType.UuidStandard || subType == BsonBinarySubType.UuidLegacy)
                {
                    bsonReader.ReturnToBookmark(binaryDataBookmark);
                    return guidSerializer.Deserialize(context);
                }

                goto default;

            case BsonType.Boolean:
                return bsonReader.ReadBoolean();

            case BsonType.DateTime:
                var millisecondsSinceEpoch = bsonReader.ReadDateTime();
                var bsonDateTime = new BsonDateTime(millisecondsSinceEpoch);
                return bsonDateTime.ToUniversalTime();

            case BsonType.Decimal128:
                return bsonReader.ReadDecimal128();

            case BsonType.Document:
                return DeserializeDiscriminatedValue(context, args);

            case BsonType.Double:
                return bsonReader.ReadDouble();

            case BsonType.Int32:
                return bsonReader.ReadInt32();

            case BsonType.Int64:
                return bsonReader.ReadInt64();

            case BsonType.Null:
                bsonReader.ReadNull();
                return null;

            case BsonType.ObjectId:
                return bsonReader.ReadObjectId();

            case BsonType.String:
            {
                var dynamicObjectAsBsonValue = BsonSerializer.Deserialize<BsonValue>(context.Reader);

                if (DateTimeOffset.TryParse(dynamicObjectAsBsonValue.AsString, out var dateTimeOffsetValue))
                    return dateTimeOffsetValue;
                if (DateTime.TryParse(dynamicObjectAsBsonValue.AsString, out var dateValue))
                    return dateValue;
                return dynamicObjectAsBsonValue.AsString;
            }

            default:
                var message = string.Format("PlatformDynamicObjectMongoDbSerializer does not support BSON type '{0}'.", bsonType);
                throw new FormatException(message);
        }
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return
            base.Equals(obj) &&
            obj is PlatformDynamicObjectMongoDbSerializer other &&
            Equals(AllowedDeserializationTypes, other.AllowedDeserializationTypes) &&
            Equals(AllowedSerializationTypes, other.AllowedSerializationTypes) &&
            Equals(DiscriminatorConvention, other.DiscriminatorConvention) &&
            GuidRepresentation.Equals(other.GuidRepresentation);
    }

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <summary>
    /// Serializes a value.
    /// </summary>
    /// <param name="context">The serialization context.</param>
    /// <param name="args">The serialization args.</param>
    /// <param name="value">The object.</param>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        var bsonWriter = context.Writer;

        if (value == null)
            bsonWriter.WriteNull();
        else
        {
            var actualType = value.GetType();
            if (actualType == typeof(object))
            {
                bsonWriter.WriteStartDocument();
                bsonWriter.WriteEndDocument();
            }
            else
            {
                // certain types can be written directly as BSON value
                // if we're not at the top level document, or if we're using the JsonWriter
                if (bsonWriter.State == BsonWriterState.Value || bsonWriter is JsonWriter)
                {
                    switch (Type.GetTypeCode(actualType))
                    {
                        case TypeCode.Boolean:
                            bsonWriter.WriteBoolean((bool)value);
                            return;

                        case TypeCode.DateTime:
                            // TODO: is this right? will lose precision after round trip
                            var bsonDateTime = new BsonDateTime(BsonUtils.ToUniversalTime((DateTime)value));
                            bsonWriter.WriteDateTime(bsonDateTime.MillisecondsSinceEpoch);
                            return;

                        case TypeCode.Double:
                            bsonWriter.WriteDouble((double)value);
                            return;

                        case TypeCode.Int16:
                            // TODO: is this right? will change type to Int32 after round trip
                            bsonWriter.WriteInt32((short)value);
                            return;

                        case TypeCode.Int32:
                            bsonWriter.WriteInt32((int)value);
                            return;

                        case TypeCode.Int64:
                            bsonWriter.WriteInt64((long)value);
                            return;

                        case TypeCode.Object:
                            if (actualType == typeof(Decimal128))
                            {
                                var decimal128 = (Decimal128)value;
                                bsonWriter.WriteDecimal128(decimal128);
                                return;
                            }

                            if (actualType == typeof(Guid))
                            {
                                var guid = (Guid)value;
                                guidSerializer.Serialize(context, args, guid);
                                return;
                            }

                            if (actualType == typeof(ObjectId))
                            {
                                bsonWriter.WriteObjectId((ObjectId)value);
                                return;
                            }

                            break;

                        case TypeCode.String:
                            bsonWriter.WriteString((string)value);
                            return;
                    }
                }

                SerializeDiscriminatedValue(context, args, value, actualType);
            }
        }
    }

    /// <summary>
    /// Returns a new PlatformDynamicObjectMongoDbSerializer configured the same but with the specified discriminator convention.
    /// </summary>
    /// <param name="discriminatorConvention">The discriminator convention.</param>
    /// <returns>An PlatformDynamicObjectMongoDbSerializer with the specified discriminator convention.</returns>
    public PlatformDynamicObjectMongoDbSerializer WithDiscriminatorConvention(IDiscriminatorConvention discriminatorConvention)
    {
        return new PlatformDynamicObjectMongoDbSerializer(discriminatorConvention, GuidRepresentation, AllowedDeserializationTypes, AllowedSerializationTypes);
    }

    // private methods
    private object DeserializeDiscriminatedValue(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonReader = context.Reader;

        var actualType = DiscriminatorConvention.GetActualType(bsonReader, typeof(object));
        if (!AllowedDeserializationTypes(actualType))
        {
            throw new BsonSerializationException(
                $"Type {actualType.FullName} is not configured as a type that is allowed to be deserialized for this instance of PlatformDynamicObjectMongoDbSerializer.");
        }

        if (actualType == typeof(object))
        {
            var type = bsonReader.GetCurrentBsonType();
            switch (type)
            {
                case BsonType.Document:
                    if (context.DynamicDocumentSerializer != null) return context.DynamicDocumentSerializer.Deserialize(context, args);
                    break;
            }

            bsonReader.ReadStartDocument();
            bsonReader.ReadEndDocument();
            return new object();
        }
        else
        {
            var serializer = BsonSerializer.LookupSerializer(actualType);
            var polymorphicSerializer = serializer as IBsonPolymorphicSerializer;
            if (polymorphicSerializer is { IsDiscriminatorCompatibleWithObjectSerializer: true })
                return serializer.Deserialize(context, args);
            else
            {
                object value = null;
                var wasValuePresent = false;

                bsonReader.ReadStartDocument();
                while (bsonReader.ReadBsonType() != 0)
                {
                    var name = bsonReader.ReadName();
                    if (name == DiscriminatorConvention.ElementName)
                        bsonReader.SkipValue();
                    else if (name == "_v")
                    {
                        value = serializer.Deserialize(context);
                        wasValuePresent = true;
                    }
                    else
                    {
                        var message = string.Format("Unexpected element name: '{0}'.", name);
                        throw new FormatException(message);
                    }
                }

                bsonReader.ReadEndDocument();

                if (!wasValuePresent) throw new FormatException("_v element missing.");

                return value;
            }
        }
    }

    private void SerializeDiscriminatedValue(BsonSerializationContext context, BsonSerializationArgs args, object value, Type actualType)
    {
        if (!AllowedSerializationTypes(actualType))
        {
            throw new BsonSerializationException(
                $"Type {actualType.FullName} is not configured as a type that is allowed to be serialized for this instance of PlatformDynamicObjectMongoDbSerializer.");
        }

        var serializer = BsonSerializer.LookupSerializer(actualType);

        var polymorphicSerializer = serializer as IBsonPolymorphicSerializer;
        if (polymorphicSerializer is { IsDiscriminatorCompatibleWithObjectSerializer: true })
            serializer.Serialize(context, args, value);
        else
        {
            if (context.IsDynamicType != null && context.IsDynamicType(value.GetType()))
            {
                args.NominalType = actualType;
                serializer.Serialize(context, args, value);
            }
            else
            {
                var bsonWriter = context.Writer;
                var discriminator = DiscriminatorConvention.GetDiscriminator(typeof(object), actualType);

                bsonWriter.WriteStartDocument();
                if (discriminator != null)
                {
                    bsonWriter.WriteName(DiscriminatorConvention.ElementName);
                    BsonValueSerializer.Instance.Serialize(context, discriminator);
                }

                bsonWriter.WriteName("_v");
                serializer.Serialize(context, value);
                bsonWriter.WriteEndDocument();
            }
        }
    }

    // nested types
    private static class DefaultFrameworkAllowedTypes
    {
        private static readonly HashSet<Type> AllowedNonGenericTypesSet =
        [
            typeof(bool),
            typeof(byte),
            typeof(char),
            typeof(ArrayList),
            typeof(BitArray),
            typeof(Hashtable),
            typeof(Queue),
            typeof(SortedList),
            typeof(ListDictionary),
            typeof(OrderedDictionary),
            typeof(Stack),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(decimal),
            typeof(double),
            typeof(ExpandoObject),
            typeof(Guid),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(DnsEndPoint),
            typeof(EndPoint),
            typeof(IPAddress),
            typeof(IPEndPoint),
            typeof(IPHostEntry),
            typeof(object),
            typeof(sbyte),
            typeof(float),
            typeof(string),
            typeof(Regex),
            typeof(TimeSpan),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(Uri),
            typeof(Version)
        ];

        private static readonly HashSet<Type> AllowedGenericTypesSet =
        [
            typeof(Dictionary<,>),
            typeof(HashSet<>),
            typeof(KeyValuePair<,>),
            typeof(LinkedList<>),
            typeof(List<>),
            typeof(Queue<>),
            typeof(SortedDictionary<,>),
            typeof(SortedList<,>),
            typeof(SortedSet<>),
            typeof(Stack<>),
            typeof(Collection<>),
            typeof(KeyedCollection<,>),
            typeof(ObservableCollection<>),
            typeof(ReadOnlyCollection<>),
            typeof(ReadOnlyDictionary<,>),
            typeof(ReadOnlyObservableCollection<>),
            typeof(Nullable<>),
            typeof(Tuple<>),
            typeof(Tuple<,>),
            typeof(Tuple<,,>),
            typeof(Tuple<,,,>),
            typeof(Tuple<,,,,>),
            typeof(Tuple<,,,,,>),
            typeof(Tuple<,,,,,,>),
            typeof(Tuple<,,,,,,,>),
            typeof(ValueTuple<,,,,,,,>),
            typeof(ValueTuple<>),
            typeof(ValueTuple<,>),
            typeof(ValueTuple<,,>),
            typeof(ValueTuple<,,,>),
            typeof(ValueTuple<,,,,>),
            typeof(ValueTuple<,,,,,>),
            typeof(ValueTuple<,,,,,,>),
            typeof(ValueTuple<,,,,,,,>)
        ];

        public static Func<Type, bool> AllowedTypes { get; } = AllowedTypesImplementation;

        private static bool AllowedTypesImplementation(Type type)
        {
            return type.IsConstructedGenericType ? IsAllowedGenericType(type) : IsAllowedType(type);

            static bool IsAllowedType(Type type) =>
                typeof(BsonValue).IsAssignableFrom(type) ||
                AllowedNonGenericTypesSet.Contains(type) ||
                (type.IsArray && AllowedTypesImplementation(type.GetElementType())) ||
                type.IsEnum;

            static bool IsAllowedGenericType(Type type) =>
                (AllowedGenericTypesSet.Contains(type.GetGenericTypeDefinition()) || IsAnonymousType(type)) &&
                type.GetGenericArguments().All(AllowedTypes);
        }

        private static bool IsAnonymousType(Type type)
        {
            // don't test for too many things in case implementation details change in the future
            return
                type.GetCustomAttributes(false).Any(x => x is CompilerGeneratedAttribute) &&
                type.IsGenericType &&
                type.Name.Contains("Anon"); // don't check for more than "Anon" so it works in mono also
        }
    }

    #region static

    // private static fields

    // public static properties
    /// <summary>
    /// An allowed types function that returns true for all types.
    /// </summary>
    public static Func<Type, bool> AllAllowedTypes { get; } = t => true;

    /// <summary>
    /// An allowed types function that returns true for framework types known to be safe.
    /// </summary>
    public static Func<Type, bool> DefaultAllowedTypes => DefaultFrameworkAllowedTypes.AllowedTypes;

    /// <summary>
    /// Gets the standard instance.
    /// </summary>
    public static PlatformDynamicObjectMongoDbSerializer Instance { get; } = new();

    /// <summary>
    /// An allowed types function that returns false for all types.
    /// </summary>
    public static Func<Type, bool> NoAllowedTypes { get; } = t => false;

    #endregion
}
