using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.DeprecatedFPLibrary;

public static partial class F
{
    public static Validation<T> Valid<T>(T value = default)
    {
        return new Validation<T>(value);
    }

    public static Validation<T> ValidateNotNull<T>(this T value, params Error[] errors)
    {
        return value != null ? new Validation<T>(value) : new Validation.Invalid(errors);
    }

    public static Validation<T> Validate<T>(this T value, bool condition, params Error[] errors)
    {
        return condition ? new Validation<T>(value) : new Validation.Invalid(errors);
    }

    public static Validation<ValueTuple> ValidIf(bool condition, params Error[] errors)
    {
        return condition ? new Validation<ValueTuple>(Unit()) : new Validation.Invalid(errors);
    }

    /// <summary>
    /// Aggregate all validations, collect all validations errors. Ex: [Val1, Val2].Combine() = Val1 & Val2 (Mean that
    /// execute both Val1 and Val2, then harvest return all errors from both all validations in list)
    /// </summary>
    public static Validation<IEnumerable<TValidation>> Aggregate<TValidation>(this IEnumerable<Validation<TValidation>> validations)
    {
        var validationsList = validations.ToList();
        var errors = validationsList.SelectMany(p => p.Errors()).ToList();
        var items = validationsList.SelectMany(p => p.AsEnumerable());
        return errors.IsEmpty() ? Valid(items) : Invalid(errors);
    }

    /// <summary>
    /// Aggregate all validations, collect all validations errors. Ex: [Val1, Val2].Combine() = Val1 & Val2 (Mean that
    /// execute both Val1 and Val2, then harvest return all errors from both all validations in list)
    /// </summary>
    public static Validation<IEnumerable<TValidation>> Aggregate<TValidation>(params Validation<TValidation>[] validations)
    {
        return Aggregate(validations.AsEnumerable());
    }

    public static Validation<ValueTuple> Combine<TValidation>(this IEnumerable<Func<Validation<TValidation>>> validations)
    {
        return validations.Aggregate(Valid(Unit()), (acc, validator) => acc.And(_ => validator().For(Unit())));
    }

    // create a Validation in the Invalid state
    public static Validation.Invalid Invalid(params Error[] errors)
    {
        return new Validation.Invalid(errors);
    }

    public static Validation<TR> Invalid<TR>(params Error[] errors)
    {
        return new Validation.Invalid(errors);
    }

    public static Validation.Invalid Invalid(IEnumerable<Error> errors)
    {
        return new Validation.Invalid(errors);
    }

    public static Validation<TR> Invalid<TR>(List<Error> errors)
    {
        return new Validation.Invalid(errors);
    }
}

public static class Validation
{
    public static List<Error> Errors<T>(this Validation<T> opt)
    {
        return opt.Match(errors => errors, _ => F.List<Error>());
    }

    public static Validation<TRr> For<TR, TRr>(
        this Validation<TR> @this,
        TRr value)
    {
        return @this.IsValid
            ? F.Valid(value)
            : F.Invalid(@this.Errors);
    }

    public static Validation<T> WithErrorMsg<T>(
        this Validation<T> @this,
        Func<List<Error>, IEnumerable<string>> fMapErrors)
    {
        return @this.IsValid
            ? @this
            : F.Invalid(fMapErrors(@this.Errors).Select(p => (Error)p));
    }

    public static Validation<TRr> Then<TR, TRr>(
        this Validation<TR> @this,
        Func<TR, TRr> f)
    {
        return @this.IsValid
            ? F.Valid(f(@this.Value))
            : F.Invalid(@this.Errors);
    }

    public static Validation<TRr> Then<TR, TRr>(
        this Validation<TR> @this,
        Func<TR, Validation<TRr>> f)
    {
        return @this.IsValid
            ? f(@this.Value)
            : F.Invalid(@this.Errors);
    }

    public static Validation<TR> And<T, TR>(
        this Validation<T> val,
        Func<T, Validation<TR>> f)
    {
        return val.Match(
            p => F.Invalid(p),
            f);
    }

    public static Validation<T> And<T>(
        this Validation<T> val,
        Func<T, bool> condition,
        params Error[] errors)
    {
        return val.Match(
            p => F.Invalid(p),
            r => val.Value.Validate(condition(r), errors));
    }

    public static Validation<TR> Then<T, TR>(
        this Validation<T> val,
        Validation<TR> valNext)
    {
        return val.Match(
            p => F.Invalid(p),
            _ => valNext);
    }

    public static async Task<Validation<TR>> Then<T, TR>(
        this Validation<T> val,
        Func<T, Task<Validation<TR>>> f)
    {
        return await val.Match(
            reasons => F.Invalid<TR>(reasons).BoxedInTask(),
            f);
    }

    public static async Task<Validation<TR>> Then<T, TR>(
        this Validation<T> @this,
        Func<T, Task<TR>> func)
    {
        return await @this.Match(
            reasons => F.Invalid<TR>(reasons).BoxedInTask(),
            t => func(t).Then(F.Valid));
    }

    public static async Task<Validation<TR>> ThenAsync<T, TR>(
        this Task<Validation<T>> valTask,
        Func<T, Task<TR>> f)
    {
        return await valTask.Then(valT => valT.Then(f));
    }

    public static async Task<Validation<TR>> ThenAsync<T, TR>(
        this Task<Validation<T>> valTask,
        Func<T, TR> f)
    {
        return await valTask.Then(valT => valT.Then(f));
    }

    public static async Task<Validation<TR>> ThenAsync<T, TR>(
        this Task<Validation<T>> valTask,
        Func<T, Task<Validation<TR>>> f)
    {
        return await valTask.Then(valT => valT.Then(f));
    }

    public static async Task<Validation<TR>> ThenAsync<T, TR>(
        this Task<Validation<T>> valTask,
        Func<T, Validation<TR>> f)
    {
        return await valTask.Then(valT => valT.And(f));
    }

    public class ValidationException : Exception
    {
        public ValidationException()
        {
            Messages = [];
        }

        public ValidationException(string message) : base(message)
        {
            Messages =
            [
                message
            ];
        }

        public ValidationException(IEnumerable<string> messages)
        {
            Messages = messages.ToList();
        }

        public ValidationException(IEnumerable<Error> messages)
        {
            Messages = messages.Select(p => p.Message).ToList();
        }

        public List<string> Messages { get; set; }
    }

    public struct Invalid
    {
        internal List<Error> Errors { get; set; }

        public Invalid(IEnumerable<Error> errors)
        {
            Errors = errors.ToList();
        }
    }
}

public readonly struct Validation<T>
{
    public List<Error> Errors { get; }
    public T Value { get; }

    public bool IsValid { get; }

    // the Return function for Validation
    public static readonly Func<T, Validation<T>> Return = F.Valid;

    public static Validation<T> Fail(List<Error> errors)
    {
        return new Validation<T>(errors);
    }

    public static Validation<T> Fail(params Error[] errors)
    {
        return new Validation<T>(errors.ToList());
    }

    private Validation(IEnumerable<Error> errors)
    {
        IsValid = false;
        Errors = errors.ToList();
        Value = default;
    }

    internal Validation(T right)
    {
        IsValid = true;
        Value = right;
        Errors = [];
    }

    public static implicit operator Validation<T>(Error error)
    {
        return new Validation<T>(Util.ListBuilder.NewArray(error));
    }

    public static implicit operator Validation<T>(Validation.Invalid left)
    {
        return new Validation<T>(left.Errors);
    }

    public static implicit operator Validation<T>(PlatformValidationResult<T> platformValidation)
    {
        return platformValidation.IsValid
            ? F.Valid(platformValidation.Value)
            : F.Invalid<T>(platformValidation.Errors.Select(p => (Error)p.ToString()).ToArray());
    }

    public static implicit operator Validation<T>(T right)
    {
        return F.Valid(right);
    }

    public static implicit operator Validation<ValueTuple>(Validation<T> target)
    {
        return target.For(F.Unit());
    }

    public static implicit operator bool(Validation<T> target)
    {
        return target.IsValid;
    }

    public static bool operator ==(Validation<T> left, Validation<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Validation<T> left, Validation<T> right)
    {
        return !(left == right);
    }

    public TR Match<TR>(Func<List<Error>, TR> invalid, Func<T, TR> valid)
    {
        return IsValid ? valid(Value) : invalid(Errors);
    }

    public IEnumerable<T> AsEnumerable()
    {
        if (IsValid) yield return Value;
    }

    public override string ToString()
    {
        return IsValid
            ? $"Valid({Value})"
            : $"Invalid: {Errors.JoinToString(", ")}.";
    }

    public override bool Equals(object obj)
    {
        if (obj is Validation<T> validationObj) return validationObj.ToString() == ToString();

        return false;
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }

    public T EnsureValid()
    {
        return IsValid ? Value : throw new Validation.ValidationException(Errors);
    }
}
