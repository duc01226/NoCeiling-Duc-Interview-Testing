using System.Linq.Expressions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;
using Easy.Platform.Domain.Entities;

namespace Easy.Platform.Domain.Exceptions.Extensions;

public static class EnsureThrowDomainExceptionExtension
{
    public static T EnsureDomainLogicValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformDomainException(val.ErrorsMsg());
    }

    public static T EnsureDomainValidationValid<T>(this PlatformValidationResult<T> val)
    {
        return val.IsValid ? val.Value : throw new PlatformDomainValidationException(val.ErrorsMsg());
    }

    public static T EnsureDomainValidationValid<T>(this T value, Func<T, bool> must, string errorMsg)
    {
        return must(value) ? value : throw new PlatformDomainValidationException(errorMsg);
    }

    public static T EnsureDomainValidationValid<T>(this T value, Func<T, Task<bool>> must, string errorMsg)
    {
        return must(value).GetResult() ? value : throw new PlatformDomainValidationException(errorMsg);
    }

    public static async Task<T> EnsureDomainLogicValid<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureDomainLogicValid();
    }

    public static async Task<T> EnsureDomainValidationValid<T>(this Task<PlatformValidationResult<T>> valTask)
    {
        var applicationVal = await valTask;
        return applicationVal.EnsureDomainValidationValid();
    }

    public static async Task<T> EnsureDomainValidationValid<T>(this Task<T> valueTask, Func<T, bool> must, string errorMsg)
    {
        var value = await valueTask;
        return must(value) ? value : throw new PlatformDomainValidationException(errorMsg);
    }

    public static void EnsureDomainValidationValid(this PlatformValidationResult validationResult)
    {
        if (validationResult is { IsValid: false })
            throw new PlatformDomainValidationException(validationResult);
    }

    public static void EnsureDomainValidationValid(this List<Func<PlatformValidationResult>> validationResultFns)
    {
        validationResultFns.ForEach(
            validationResultFn =>
            {
                var validationResult = validationResultFn();
                if (validationResult?.IsValid == false)
                    throw new PlatformDomainValidationException(validationResult);
            });
    }

    public static async Task EnsureDomainValidationValid(this Task<PlatformValidationResult> validationResultTask)
    {
        if (validationResultTask != null)
        {
            var validationResult = await validationResultTask;

            if (validationResult?.IsValid == false)
                throw new PlatformDomainValidationException(validationResult);
        }
    }

    public static async Task EnsureDomainValidationValid(this List<Func<Task<PlatformValidationResult>>> validateActions)
    {
        await validateActions.ForEachAsync(
            async validateAction =>
            {
                var validationResult = await validateAction();

                if (validationResult?.IsValid == false)
                    throw new PlatformDomainValidationException(validationResult);
            });
    }

    public static async Task EnsureEntityValid<TEntity, TPrimaryKey>(
        this TEntity entity,
        Func<Expression<Func<TEntity, bool>>, CancellationToken, Task<bool>> anyAsyncFunc,
        CancellationToken cancellationToken) where TEntity : IEntity<TPrimaryKey>
    {
        if (entity is IValidatableEntity<TEntity, TPrimaryKey> validatableEntity)
        {
            validatableEntity.Validate().EnsureValid();

            await EnsureEntityUnique(validatableEntity, anyAsyncFunc, cancellationToken);
        }
    }

    private static async Task EnsureEntityUnique<TEntity, TPrimaryKey>(
        IValidatableEntity<TEntity, TPrimaryKey> validatableEntity,
        Func<Expression<Func<TEntity, bool>>, CancellationToken, Task<bool>> anyAsyncFunc,
        CancellationToken cancellationToken)
        where TEntity : IEntity<TPrimaryKey>
    {
        var entityCheckUniquenessValidator = validatableEntity.CheckUniqueValidator();

        if (entityCheckUniquenessValidator != null)
            await entityCheckUniquenessValidator.Validate(predicate => anyAsyncFunc(predicate, cancellationToken)).EnsureDomainValidationValid();
    }

    public static async Task EnsureEntitiesValid<TEntity, TPrimaryKey>(
        this List<TEntity> entities,
        Func<Expression<Func<TEntity, bool>>, CancellationToken, Task<bool>> anyAsyncFunc,
        CancellationToken cancellationToken) where TEntity : IEntity<TPrimaryKey>
    {
        EnsureEntitiesValid<TEntity, TPrimaryKey>(entities);

        await EnsureEntitiesUnique<TEntity, TPrimaryKey>(entities, anyAsyncFunc, cancellationToken);
    }

    public static async Task EnsureEntitiesUnique<TEntity, TPrimaryKey>(
        this List<TEntity> entities,
        Func<Expression<Func<TEntity, bool>>, CancellationToken, Task<bool>> anyAsyncFunc,
        CancellationToken cancellationToken) where TEntity : IEntity<TPrimaryKey>
    {
        // Validate each IValidatableEntity with CheckUniquenessValidator != null must be unique in the existing in database items
        // and also in the list items itself
        var validateEntityUniqueActions = entities
            .OfType<IValidatableEntity<TEntity, TPrimaryKey>>()
            .Where(entity => entity.CheckUniqueValidator() != null)
            .Select<IValidatableEntity<TEntity, TPrimaryKey>, Func<Task<PlatformValidationResult>>>(
                entity =>
                    () => entity.CheckUniqueValidator().Validate(
                        checkAnyDuplicatedItemAsyncFunction: async findOtherDuplicatedItemPredicate =>
                            entities.Any(findOtherDuplicatedItemPredicate.Compile()) ||
                            await anyAsyncFunc(findOtherDuplicatedItemPredicate, cancellationToken)))
            .ToList();

        await validateEntityUniqueActions.EnsureDomainValidationValid();
    }

    public static void EnsureEntitiesValid<TEntity, TPrimaryKey>(this List<TEntity> entities) where TEntity : IEntity<TPrimaryKey>
    {
        entities.ForEach(
            entity =>
            {
                if (entity is IValidatableEntity<TEntity, TPrimaryKey> validatableEntity)
                    validatableEntity.Validate().EnsureDomainValidationValid();
            });
    }
}
