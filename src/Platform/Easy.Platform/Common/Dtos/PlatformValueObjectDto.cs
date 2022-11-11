using Easy.Platform.Common.Validations;
using Easy.Platform.Common.ValueObjects.Abstract;

namespace Easy.Platform.Common.Dtos;

public abstract class PlatformValueObjectDto<TValueObject> : IPlatformDto<PlatformValueObjectDto<TValueObject>, TValueObject>
    where TValueObject : class, IPlatformValueObject<TValueObject>
{
    public abstract TValueObject MapToObject();

    public virtual PlatformValidationResult<PlatformValueObjectDto<TValueObject>> Validate()
    {
        return MapToObject().Validate().Of(this);
    }
}
