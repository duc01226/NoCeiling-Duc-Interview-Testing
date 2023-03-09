using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.DeprecatedFPLibrary;

public class Option
{
    public readonly struct None
    {
        public static readonly None Default = new();
    }

    public readonly struct Some<T>
    {
        internal T Value { get; }

        internal Some(T value)
        {
            if (value == null)
                throw new ArgumentNullException(
                    nameof(value),
                    "Cannot wrap a null value in a 'Some'; use 'None' instead");

            Value = value;
        }
    }
}

public readonly struct Option<T> : IEquatable<Option.None>, IEquatable<Option<T>>
{
    private readonly T value;

    private Option(T value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        IsSome = true;
        this.value = value;
    }

    public static implicit operator Option<T>(Option.None none)
    {
        return default;
    }

    public static implicit operator Option<T>(Option.Some<T> some)
    {
        return new Option<T>(some.Value);
    }

    public static bool operator ==(Option<T> @this, Option<T> other)
    {
        return @this.Equals(other);
    }

    public static bool operator !=(Option<T> @this, Option<T> other)
    {
        return !(@this == other);
    }

    public bool IsSome { get; }

    public bool IsNone => !IsSome;

    public T Value => value;

    public TR Match<TR>(Func<TR> none, Func<T, TR> some)
    {
        return IsSome ? some(value) : none();
    }

    public void Match(Action none, Action<T> some)
    {
        if (IsSome)
            some(value);
        else
            none();
    }

    public IEnumerable<T> AsEnumerable()
    {
        if (IsSome) yield return value;
    }

    public bool Equals(Option<T> other)
    {
        return IsSome == other.IsSome && (IsNone || value.Equals(other.value));
    }

    public bool Equals(Option.None target)
    {
        return IsNone;
    }

    public T GetValue()
    {
        return AsEnumerable().First();
    }

    public override string ToString()
    {
        return IsSome ? $"Some({value})" : "None";
    }

    public override bool Equals(object obj)
    {
        if (obj is Option<T> objOption) return Equals(objOption);

        return false;
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }

    public TR MatchValue<TR>(TR none, Func<T, TR> some)
    {
        return IsSome ? some(Value) : none;
    }
}

public static class OptionExt
{
    public static bool NullOrWhiteSpace(this Option<string> optionString)
    {
        return optionString.Match(
            () => true,
            string.IsNullOrWhiteSpace);
    }

    public static Validation<T> ToValidation<T>(this Option<T> opt, Error error)
    {
        return opt.Match(
            () => F.Invalid(error),
            F.Valid);
    }

    public static Task<Validation<T>> ToValidationAsync<T>(this Task<Option<T>> optTask, Error error)
    {
        return optTask.Then(_ => _.ToValidation(error));
    }

    public static Task<TR> MatchAsync<T, TR>(this Task<Option<T>> optTask, Func<TR> none, Func<T, TR> some)
    {
        return optTask.Then(opt => opt.Match(none, some));
    }

    public static Task<TR> MatchAsync<T, TR>(
        this Task<Option<T>> optTask,
        Func<Task<TR>> none,
        Func<T, Task<TR>> some)
    {
        return optTask.Then(opt => opt.Match(none, some));
    }

    public static Task<TR> MatchAsync<T, TR>(this Task<Option<T>> optTask, Func<Task<TR>> none, Func<T, TR> some)
    {
        return optTask.Then(opt => opt.Match(none, _ => some(_).AsTask()));
    }

    public static Task<Validation<ValueTuple>> ValidateIfSomeAsync<T>(
        this Option<T> opt,
        Func<T, Task<Validation<ValueTuple>>> validateFn)
    {
        return opt.Match(
            () => F.Valid(F.Unit()).AsTask(),
            t => validateFn(t));
    }

    public static Task<Validation<ValueTuple>> ValidateIfSomeAsync<T>(
        this Task<Option<T>> opt,
        Func<T, Task<Validation<ValueTuple>>> validateFn)
    {
        return opt.Then(optionT => optionT.ValidateIfSomeAsync(validateFn));
    }

    public static Option<T> Where<T>(
        this Option<T> optT,
        Func<T, bool> predicate)
    {
        return optT.Match(
            () => F.None,
            t => predicate(t) ? optT : F.None);
    }

    public static Option<T> Lookup<TK, T>(this IDictionary<TK, T> dict, TK key)
    {
        return dict.TryGetValue(key, out var value) ? F.Some(value) : F.None;
    }

    public static Option<T> AsOption<T>(this T value)
    {
        return value == null ? F.None : F.Some(value);
    }

    public static Option<T> AsOption<T>(this T? value) where T : struct
    {
        return value == null ? F.None : F.Some(value.Value);
    }
}
