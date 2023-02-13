using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.ValueObjects.Abstract;

public interface IPlatformValueObject<TValueObject> : IEquatable<TValueObject>
    where TValueObject : IPlatformValueObject<TValueObject>
{
    bool Equals(object obj);
    int GetHashCode();
    PlatformValidationResult<TValueObject> Validate();
    string ToString();
}

public abstract class PlatformValueObject<TValueObject> : IPlatformValueObject<TValueObject>, IEquatable<PlatformValueObject<TValueObject>>
    where TValueObject : PlatformValueObject<TValueObject>, new()
{
    public bool Equals(PlatformValueObject<TValueObject> other)
    {
        return ToString() == other?.ToString();
    }

    public override bool Equals(object obj)
    {
        return ToString() == obj?.ToString();
    }

    public bool Equals(TValueObject other)
    {
        return ToString() == other?.ToString();
    }

    public override int GetHashCode()
    {
        return ToString()?.GetHashCode() ?? -1;
    }

    /// <summary>
    /// To a unique string present this object value
    /// </summary>
    public override string ToString()
    {
        return PlatformJsonSerializer.Serialize(this);
    }

    public virtual PlatformValidationResult<TValueObject> Validate()
    {
        return PlatformValidationResult<TValueObject>.Valid();
    }

    public static bool operator ==(PlatformValueObject<TValueObject> lhs, PlatformValueObject<TValueObject> rhs)
    {
        return lhs?.ToString() == rhs?.ToString();
    }

    public static bool operator !=(PlatformValueObject<TValueObject> lhs, PlatformValueObject<TValueObject> rhs)
    {
        return !(lhs == rhs);
    }
}
