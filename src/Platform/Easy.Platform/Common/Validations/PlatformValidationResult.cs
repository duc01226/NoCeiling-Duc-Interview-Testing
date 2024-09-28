using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Exceptions;
using Easy.Platform.Common.Validations.Extensions;
using FluentValidation.Results;

namespace Easy.Platform.Common.Validations;

/// <summary>
/// Represents the result of a platform validation operation.
/// </summary>
/// <typeparam name="TValue">The type of the value that was validated.</typeparam>
/// <remarks>
/// This class provides methods for validating a value and returning a result that indicates whether the validation was successful or not.
/// If the validation was not successful, the result contains a list of errors.
/// </remarks>
public class PlatformValidationResult<TValue> : ValidationResult
{
    private List<PlatformValidationError> finalCombinedValidationErrors;

    public PlatformValidationResult()
    {
    }

    public PlatformValidationResult(
        TValue value,
        List<PlatformValidationError> failures,
        Func<PlatformValidationResult<TValue>, Exception> invalidException = null) : base(
        failures ?? [])
    {
        Value = value;
        RootValidationErrors = failures ?? [];
        InvalidException = invalidException;
    }

    public override bool IsValid => !Errors.Any();

    public TValue Value { get; protected set; }
    public Func<PlatformValidationResult<TValue>, Exception> InvalidException { get; set; }
    public new List<PlatformValidationError> Errors => finalCombinedValidationErrors ??= FinalCombinedValidation().RootValidationErrors;

    protected List<PlatformValidationError> RootValidationErrors { get; } = [];
    protected bool IsRootValidationValid => !RootValidationErrors.Any();

    protected List<LogicalAndValidationsChainItem> LogicalAndValidationsChain { get; set; } = [];

    /// <summary>
    /// Dictionary map from LogicalAndValidationsChainItem Position to ExceptionCreatorFn
    /// </summary>
    protected Dictionary<int, Func<PlatformValidationResult<TValue>, Exception>> LogicalAndValidationsChainInvalidExceptions { get; } =
        [];

    private PlatformValidationResult<TValue> FinalCombinedValidation()
    {
        return StandaloneRootValidation()
            .Pipe(selfValidation => IsRootValidationValid ? CombinedLogicalAndValidationsChain() : selfValidation);
    }

    private PlatformValidationResult<TValue> CombinedLogicalAndValidationsChain()
    {
        return LogicalAndValidationsChain
            .Aggregate(
                Valid(Value),
                (prevVal, nextValChainItem) => prevVal.IsValid ? nextValChainItem.ValidationFn(prevVal.Value) : prevVal,
                valResult => valResult);
    }

    private PlatformValidationResult<TValue> StandaloneRootValidation()
    {
        return RootValidationErrors.Any() ? Invalid(Value, RootValidationErrors.ToArray()) : Valid(Value);
    }

    public static implicit operator TValue(PlatformValidationResult<TValue> validation)
    {
        return validation.EnsureValid();
    }

    public static implicit operator bool(PlatformValidationResult<TValue> validation)
    {
        return validation.IsValid;
    }

    public static implicit operator string(PlatformValidationResult<TValue> validation)
    {
        return validation.ToString();
    }

    public static implicit operator PlatformValidationResult<TValue>(
        (TValue value, string error) invalidValidationInfo)
    {
        return Invalid(value: invalidValidationInfo.value, invalidValidationInfo.error);
    }

    public static implicit operator PlatformValidationResult<TValue>(
        (TValue value, List<string> errors) invalidValidationInfo)
    {
        return invalidValidationInfo.errors?.Any() == true
            ? Invalid(
                value: invalidValidationInfo.value,
                invalidValidationInfo.errors.Select(p => (PlatformValidationError)p).ToArray())
            : Valid(invalidValidationInfo.value);
    }

    public static implicit operator PlatformValidationResult<TValue>(
        (TValue value, List<PlatformValidationError> errors) invalidValidationInfo)
    {
        return invalidValidationInfo.errors?.Any() == true
            ? Invalid(value: invalidValidationInfo.value, invalidValidationInfo.errors.Select(p => p).ToArray())
            : Valid(invalidValidationInfo.value);
    }

    public static implicit operator PlatformValidationResult(
        PlatformValidationResult<TValue> validation)
    {
        return new PlatformValidationResult(validation.Value, validation.Errors);
    }

    /// <summary>
    /// Return a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    internal static PlatformValidationResult<TValue> Valid(TValue value = default)
    {
        return new PlatformValidationResult<TValue>(value, null);
    }

    /// <summary>
    /// Return a invalid validation result.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A invalid validation result.</returns>
    internal static PlatformValidationResult<TValue> Invalid(
        TValue value,
        params PlatformValidationError[] errors)
    {
        return errors?.Any() == true
            ? new PlatformValidationResult<TValue>(value, errors.ToList())
            : new PlatformValidationResult<TValue>(
                value,
                Util.ListBuilder.New<PlatformValidationError>("Invalid!"));
    }

    /// <summary>
    /// Return a valid validation result if the condition is true, otherwise return a invalid validation with errors.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="must">The valid condition.</param>
    /// <param name="errors">The errors if the valid condition is false.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> Validate(
        TValue value,
        bool must,
        params PlatformValidationError[] errors)
    {
        return Validate(value, () => must, errors);
    }

    /// <inheritdoc cref="Validate(TValue,bool,Easy.Platform.Common.Validations.PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> Validate(
        TValue value,
        Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return must() ? Valid(value) : Invalid(value, errors);
    }

    /// <summary>
    /// Return a invalid validation result with errors if the condition is true, otherwise return a valid.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="mustNot">The invalid condition.</param>
    /// <param name="errors">The errors if the invalid condition is true.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> ValidateNot(
        TValue value,
        bool mustNot,
        params PlatformValidationError[] errors)
    {
        return ValidateNot(value, () => mustNot, errors);
    }

    /// <inheritdoc cref="ValidateNot(TValue,bool,Easy.Platform.Common.Validations.PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> ValidateNot(
        TValue value,
        Func<bool> mustNot,
        params PlatformValidationError[] errors)
    {
        return mustNot() ? Invalid(value, errors) : Valid(value);
    }

    /// <summary>
    /// Combine all validation but fail fast. Ex: [Val1, Val2].Combine() = Val1 && Val2
    /// </summary>
    public static PlatformValidationResult<TValue> Combine(
        params Func<PlatformValidationResult<TValue>>[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate((prevVal, nextVal) => () => prevVal().ThenValidate(p => nextVal()))();
    }

    /// <inheritdoc cref="Combine(System.Func{Easy.Platform.Common.Validations.PlatformValidationResult{TValue}}[])" />
    public static PlatformValidationResult<TValue> Combine(
        params PlatformValidationResult<TValue>[] validations)
    {
        return Combine(validations.Select(p => (Func<PlatformValidationResult<TValue>>)(() => p)).ToArray());
    }

    /// <summary>
    /// Aggregate all validations, collect all validations errors. Ex: [Val1, Val2].Combine() = Val1 & Val2 (Mean that
    /// execute both Val1 and Val2, then harvest return all errors from both all validations in list)
    /// </summary>
    public static PlatformValidationResult<TValue> Aggregate(
        params PlatformValidationResult<TValue>[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate(
                (prevVal, nextVal) => new PlatformValidationResult<TValue>(nextVal.Value, prevVal.Errors.Concat(nextVal.Errors).ToList()));
    }

    /// <inheritdoc cref="Aggregate(Easy.Platform.Common.Validations.PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult<TValue> Aggregate(
        TValue value,
        params (bool, PlatformValidationError)[] validations)
    {
        return Aggregate(
            validations
                .Select(validationInfo => Validate(value, must: validationInfo.Item1, errors: validationInfo.Item2))
                .ToArray());
    }

    /// <inheritdoc cref="Aggregate(Easy.Platform.Common.Validations.PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult<TValue> Aggregate(
        params Func<PlatformValidationResult<TValue>>[] validations)
    {
        return Aggregate(validations.Select(p => p()).ToArray());
    }

    public string ErrorsMsg()
    {
        return Errors?.Aggregate(
            string.Empty,
            (currentMsg, error) => $"{(currentMsg == string.Empty ? string.Empty : $"{currentMsg}; ")}{error}");
    }

    public override string ToString()
    {
        return ErrorsMsg();
    }

    public PlatformValidationResult<T> Then<T>(
        Func<TValue, T> next)
    {
        return Match(
            valid: value => new PlatformValidationResult<T>(next(value), null),
            invalid: err => Of<T>(default));
    }

    /// <summary>
    /// Performs an additional asynchronous validation operation on the value using the specified nextVal function if this validation is valid.
    /// </summary>
    /// <remarks>
    /// The ThenAsync method in the PlatformValidationResult[TValue] class is used to perform an additional asynchronous validation operation on the value if the current validation is valid.
    /// <br />
    /// This method takes a function nextVal as a parameter, which returns a Task[PlatformValidationResult[T]]. If the current validation is valid, it executes the nextVal function. If the current validation is not valid, it returns a new PlatformValidationResult[T] with a default value wrapped in a Task.
    /// <br />
    /// This method is part of a chainable validation mechanism, allowing you to perform a series of validations in a fluent manner. It's particularly useful when you need to perform some asynchronous operation as part of your validation process, such as making a network request or querying a database.
    /// </remarks>
    public Task<PlatformValidationResult<T>> ThenAsync<T>(
        Func<Task<PlatformValidationResult<T>>> nextVal)
    {
        return MatchAsync(
            valid: value => nextVal(),
            invalid: err => Of<T>(default).BoxedInTask());
    }

    /// <inheritdoc cref="ThenAsync{T}(Func{Task{PlatformValidationResult{T}}})" />
    public Task<PlatformValidationResult<T>> ThenAsync<T>(
        Func<TValue, Task<PlatformValidationResult<T>>> nextVal)
    {
        return MatchAsync(
            valid: value => nextVal(Value),
            invalid: err => Of<T>(default).BoxedInTask());
    }

    /// <inheritdoc cref="ThenAsync{T}(Func{Task{PlatformValidationResult{T}}})" />
    public Task<PlatformValidationResult<T>> ThenAsync<T>(
        Func<TValue, Task<T>> next)
    {
        return MatchAsync(
            valid: async value => new PlatformValidationResult<T>(await next(value), null),
            invalid: err => Of<T>(default).BoxedInTask());
    }

    /// <summary>
    /// Executes a specified function based on whether the validation result is valid or invalid.
    /// </summary>
    public PlatformValidationResult<T> Match<T>(
        Func<TValue, PlatformValidationResult<T>> valid,
        Func<IEnumerable<PlatformValidationError>, PlatformValidationResult<T>> invalid)
    {
        return IsValid ? valid(Value) : invalid(Errors);
    }

    /// <summary>
    /// Executes a specified asynchronous function based on whether the validation result is valid or invalid.
    /// </summary>
    public async Task<PlatformValidationResult<T>> MatchAsync<T>(
        Func<TValue, Task<PlatformValidationResult<T>>> valid,
        Func<IEnumerable<PlatformValidationError>, Task<PlatformValidationResult<T>>> invalid)
    {
        return IsValid ? await valid(Value) : await invalid(Errors);
    }

    /// <summary>
    /// Adds a new validation to the logical AND validation chain.
    /// </summary>
    /// <param name="nextValidation">The validation function to be added to the chain.</param>
    /// <returns>The current instance of PlatformValidationResult.</returns>
    /// <remarks>
    /// The And method in the PlatformValidationResult[TValue] class is used to chain multiple validation rules together using logical AND operation. This method is part of a fluent interface that allows you to chain multiple validations together in a readable and maintainable way.
    /// <br />
    /// In the context of the PlatformValidationResult[TValue] class, the And method takes a Func[TValue, PlatformValidationResult[TValue]] as a parameter, which represents the next validation function to be added to the validation chain.
    /// <br />
    /// The method adds the new validation to the LogicalAndValidationsChain and then returns the current instance of PlatformValidationResult[TValue], allowing further chaining of validations.
    /// </remarks>
    public PlatformValidationResult<TValue> And(Func<TValue, PlatformValidationResult<TValue>> nextValidation)
    {
        LogicalAndValidationsChain.Add(
            new LogicalAndValidationsChainItem
            {
                ValidationFn = nextValidation,
                Position = LogicalAndValidationsChain.Count
            });
        return this;
    }

    /// <summary>
    /// Chains another validation rule to the current validation result.
    /// </summary>
    /// <param name="must">A function that represents the validation rule to be applied to the value of type TValue.</param>
    /// <param name="errors">An array of PlatformValidationError objects that are added to the validation result if the validation rule is not satisfied.</param>
    /// <returns>A PlatformValidationResult object that includes the result of the new validation rule along with any previous validation results.</returns>
    /// <remarks>
    /// The And method in the PlatformValidationResult[TValue] class is used to chain multiple validation rules together. This method takes a function that returns a boolean value and an array of PlatformValidationError objects. The function is a validation rule that should be applied to the value of type TValue. If the rule is not satisfied (i.e., the function returns false), the validation errors are added to the PlatformValidationResult.
    /// <br />
    /// In the context of the provided code, the And method is used to add additional validation rules to the PlatformValidationResult object returned by the Validate method. Each call to And adds a new validation rule that the TValue object must satisfy. If any of the rules are not satisfied, the corresponding error messages are stored in the PlatformValidationResult object.
    /// <br />
    /// This approach allows for a fluent and readable way to define a series of validation rules for an object.
    /// </remarks>
    public PlatformValidationResult<TValue> And(
        Func<TValue, bool> must,
        params PlatformValidationError[] errors)
    {
        return And(() => Validate(value: Value, () => must(Value), errors));
    }

    public Task<PlatformValidationResult<TValue>> AndAsync(
        Func<TValue, Task<bool>> must,
        params PlatformValidationError[] errors)
    {
        return AndAsync(v => v.ValidateAsync(must, errors));
    }

    public PlatformValidationResult<TValue> And(
        Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return And(() => Validate(value: Value, must, errors));
    }

    /// <summary>
    /// The And method in the PlatformValidationResult[TValue] class is used to chain multiple validation rules together. It takes a Func[PlatformValidationResult[TValue]] as a parameter, which represents the next validation rule to be applied.
    /// <br />
    /// In the context of a validation process, the And method allows you to create a sequence of validation rules that will be checked one after the other. If a validation rule fails, the subsequent rules in the chain will not be executed, and the validation process will stop, returning the validation result up to that point.
    /// <br />
    /// This method is particularly useful when you have complex objects that need to be validated against multiple conditions, and you want to stop the validation process as soon as one of the conditions is not met. It helps to keep the validation logic clean and easy to follow.
    /// </summary>
    public PlatformValidationResult<TValue> And(Func<PlatformValidationResult<TValue>> nextValidation)
    {
        return And(value => nextValidation());
    }

    /// <summary>
    /// Validation[T] => and Validation[T1] => Validation[T1]
    /// </summary>
    public PlatformValidationResult<TNextValidation> ThenValidate<TNextValidation>(Func<TValue, PlatformValidationResult<TNextValidation>> nextValidation)
    {
        return IsValid ? nextValidation(Value) : PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray());
    }

    public PlatformValidationResult<TValue> And<TNextValidation>(Func<TValue, PlatformValidationResult<TNextValidation>> nextValidation)
    {
        return IsValid ? nextValidation(Value).Of(Value) : Invalid(default, Errors.ToArray());
    }

    public async Task<PlatformValidationResult<TValue>> And(
        Task<PlatformValidationResult<TValue>> nextValidation)
    {
        return !IsValid ? this : await nextValidation;
    }

    /// <summary>
    /// The AndAsync method in the PlatformValidationResult[TValue] class is used for chaining asynchronous validation operations. It takes a Func[TValue, Task[PlatformValidationResult[TValue]]] as a parameter, which represents the next validation operation to be performed.
    /// <br />
    /// If the current validation result (this) is invalid (!IsValid), it short-circuits the validation chain and returns the current result. If the current result is valid, it proceeds to execute the next validation operation (nextValidation(Value)), passing the current value (Value) to it.
    /// <br />
    /// This method is useful in scenarios where multiple validation checks need to be performed in sequence, and each check depends on the validity of the previous ones. For example, in the ValidateCanBeSavedAsync method of AttendanceRequest and LeaveRequest classes, multiple validation operations are chained using the AndAsync method. If any validation operation fails, the subsequent ones are not executed, which can save computational resources and time.
    /// </summary>
    public async Task<PlatformValidationResult<TValue>> AndAsync(
        Func<TValue, Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return !IsValid ? this : await nextValidation(Value);
    }

    /// <summary>
    /// Validation[T] => and => Validation[T1]
    /// </summary>
    public async Task<PlatformValidationResult<TNextValidation>> ThenValidateAsync<TNextValidation>(
        Func<TValue, Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray()) : await nextValidation(Value);
    }

    public async Task<PlatformValidationResult<TValue>> AndAsync<TNextValidation>(
        Func<TValue, Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid ? Invalid(default, Errors.ToArray()) : await nextValidation(Value).Then(nextValResult => nextValResult.Of(Value));
    }

    public PlatformValidationResult<TValue> AndNot(
        Func<TValue, bool> mustNot,
        params PlatformValidationError[] errors)
    {
        return And(() => ValidateNot(value: Value, () => mustNot(Value), errors));
    }

    public Task<PlatformValidationResult<TValue>> AndNotAsync(
        Func<TValue, Task<bool>> mustNot,
        params PlatformValidationError[] errors)
    {
        return AndAsync(v => v.ValidateNotAsync(mustNot, errors));
    }

    public PlatformValidationResult<TValue> Or(Func<PlatformValidationResult<TValue>> nextValidation)
    {
        return IsValid ? this : nextValidation();
    }

    public async Task<PlatformValidationResult<TValue>> Or(
        Task<PlatformValidationResult<TValue>> nextValidation)
    {
        return IsValid ? this : await nextValidation;
    }

    public async Task<PlatformValidationResult<TValue>> Or(
        Func<Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return IsValid ? this : await nextValidation();
    }

    /// <summary>
    /// Throws an exception if the validation result is invalid. It returns the validated value if the result is valid.
    /// </summary>
    public TValue EnsureValid(Func<PlatformValidationResult<TValue>, Exception> invalidException = null)
    {
        if (!IsValid)
            throw invalidException != null
                ? invalidException(this)
                : InvalidException != null
                    ? InvalidException(this)
                    : new PlatformValidationException(this);

        return Value;
    }

    public PlatformValidationResult<T> Of<T>(T value)
    {
        return new PlatformValidationResult<T>(
            value,
            Errors,
            InvalidException != null ? val => InvalidException(this) : null);
    }

    public PlatformValidationResult<T> Of<T>()
    {
        return new PlatformValidationResult<T>(
            Value.Cast<T>(),
            Errors,
            InvalidException != null ? val => InvalidException(this) : null);
    }

    /// <summary>
    /// Use this to set the specific exception for the current validation chain. So that when use ensure valid, each validation condition chain could throw the attached exception
    /// </summary>
    public PlatformValidationResult<TValue> WithInvalidException(Func<PlatformValidationResult<TValue>, Exception> invalidException)
    {
        if (!LogicalAndValidationsChain.Any())
            InvalidException = invalidException;
        else
            LogicalAndValidationsChain
                .Where(
                    andValidationsChainItem => !LogicalAndValidationsChainInvalidExceptions.ContainsKey(andValidationsChainItem.Position))
                .Ensure(
                    notSetInvalidExceptionAndValidations => notSetInvalidExceptionAndValidations.Any(),
                    "All InvalidException has been set")
                .ForEach(
                    notSetInvalidExceptionAndValidation =>
                        LogicalAndValidationsChainInvalidExceptions.Add(notSetInvalidExceptionAndValidation.Position, invalidException));

        return this;
    }

    /// <summary>
    /// Do validation all conditions in AndConditions Chain when .And().And() and collect all errors
    /// <br />
    /// "andValidationChainItem => Util.TaskRunner.CatchException(" Explain:
    /// Because the and chain could depend on previous chain, so do harvest validation errors could throw errors on some
    /// depended validation, so we catch and ignore the depended chain item
    /// try to get all available interdependent/not depend on prev validation item chain validations
    /// </summary>
    public List<PlatformValidationError> AggregateErrors()
    {
        return Util.ListBuilder.New(StandaloneRootValidation().Errors.ToArray())
            .Concat(
                LogicalAndValidationsChain.SelectMany(
                    andValidationChainItem => Util.TaskRunner.CatchException(
                        () => andValidationChainItem.ValidationFn(Value).Errors,
                        [])))
            .ToList();
    }

    public class LogicalAndValidationsChainItem
    {
        public Func<TValue, PlatformValidationResult<TValue>> ValidationFn { get; set; }
        public int Position { get; set; }
    }
}

public class PlatformValidationResult : PlatformValidationResult<object>
{
    public PlatformValidationResult(
        object value,
        List<PlatformValidationError> failures,
        Func<PlatformValidationResult<object>, Exception> invalidException = null) : base(value: value, failures, invalidException)
    {
    }

    public static implicit operator PlatformValidationResult(string error)
    {
        return Invalid<object>(null, error);
    }

    public static implicit operator PlatformValidationResult(List<string> errors)
    {
        return Invalid<object>(null, errors?.Select(p => (PlatformValidationError)p).ToArray());
    }

    public static implicit operator PlatformValidationResult(List<PlatformValidationError> errors)
    {
        return Invalid<object>(null, errors.ToArray());
    }

    /// <summary>
    /// Return a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    public static PlatformValidationResult<TValue> Valid<TValue>(TValue value = default)
    {
        return new PlatformValidationResult<TValue>(value, null);
    }

    /// <summary>
    /// Return a invalid validation result.
    /// </summary>
    /// <param name="value">The validation target object.</param>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A invalid validation result.</returns>
    public static PlatformValidationResult<TValue> Invalid<TValue>(
        TValue value,
        params PlatformValidationError[] errors)
    {
        return errors?.Any(p => p?.ToString().IsNotNullOrEmpty() == true) == true
            ? new PlatformValidationResult<TValue>(value, errors.ToList())
            : new PlatformValidationResult<TValue>(
                value,
                Util.ListBuilder.New<PlatformValidationError>("Invalid!"));
    }

    /// <summary>
    /// Return a valid validation result if the condition is true, otherwise return a invalid validation with errors.
    /// </summary>
    /// <param name="value">value</param>
    /// <param name="must">The valid condition.</param>
    /// <param name="errors">The errors if the valid condition is false.</param>
    /// <returns>A validation result.</returns>
    public static PlatformValidationResult<TValue> Validate<TValue>(
        TValue value,
        bool must,
        params PlatformValidationError[] errors)
    {
        return must ? Valid(value: value) : Invalid(value: value, errors);
    }

    /// <inheritdoc cref="Validate{TValue}(TValue,bool,Easy.Platform.Common.Validations.PlatformValidationError[])" />
    public static PlatformValidationResult<TValue> Validate<TValue>(
        TValue value,
        Func<bool> validConditionFunc,
        params PlatformValidationError[] errors)
    {
        return Validate(value, validConditionFunc(), errors);
    }

    /// <inheritdoc cref="PlatformValidationResult{TValue}.Combine(Func{PlatformValidationResult{TValue}}[])" />
    public static PlatformValidationResult Combine(
        params Func<PlatformValidationResult>[] validations)
    {
        return validations.Aggregate(
            seed: Valid(validations[0]().Value),
            (acc, validator) => acc.ThenValidate(p => validator()));
    }

    /// <inheritdoc cref="PlatformValidationResult{TValue}.Aggregate(PlatformValidationResult{TValue}[])" />
    public static PlatformValidationResult Aggregate(
        params PlatformValidationResult[] validations)
    {
        return validations.IsEmpty()
            ? Valid()
            : validations.Aggregate(
                (prevVal, nextVal) => new PlatformValidationResult(nextVal.Value, prevVal.Errors.Concat(nextVal.Errors).ToList()));
    }

    public PlatformValidationResult And(Func<PlatformValidationResult> nextValidation)
    {
        return IsValid ? nextValidation() : Invalid(Value, Errors.ToArray());
    }

    public async Task<PlatformValidationResult> AndAsync(
        Func<Task<PlatformValidationResult>> nextValidation)
    {
        return !IsValid ? this : await nextValidation();
    }

    public async Task<PlatformValidationResult<TNextValidation>> AndThenAsync<TNextValidation>(
        Func<Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray()) : await nextValidation();
    }

    public async Task<PlatformValidationResult<object>> AndAsync<TNextValidation>(
        Func<Task<PlatformValidationResult<TNextValidation>>> nextValidation)
    {
        return !IsValid
            ? PlatformValidationResult<TNextValidation>.Invalid(default, Errors.ToArray())
            : await nextValidation().Then(nextValResult => nextValResult.Of(Value));
    }

    public TValue EnsureValid<TValue>(Func<PlatformValidationResult, Exception> invalidException = null)
    {
        return EnsureValid(invalidException != null ? _ => invalidException(this) : null).Cast<TValue>();
    }
}
