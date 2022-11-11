using System.Linq.Expressions;

namespace Easy.Platform.Common.Validations.Validators;

public class PlatformCheckUniquenessValidator<T> : PlatformValidator<T>
{
    public PlatformCheckUniquenessValidator(
        T targetItem,
        Expression<Func<T, bool>> findOtherDuplicatedItemExpr,
        string errorMessage)
    {
        TargetItem = targetItem;
        FindOtherDuplicatedItemExpr = findOtherDuplicatedItemExpr;
        ErrorMessage = errorMessage;
    }

    public T TargetItem { get; }

    public Expression<Func<T, bool>> FindOtherDuplicatedItemExpr { get; }

    public string ErrorMessage { get; }

    public PlatformValidationResult Validate(Func<Expression<Func<T, bool>>, bool> checkAnyDuplicatedItemFunction)
    {
        return checkAnyDuplicatedItemFunction(FindOtherDuplicatedItemExpr)
            ? Invalid(TargetItem, "", ErrorMessage)
            : Valid(TargetItem);
    }

    public async Task<PlatformValidationResult> Validate(
        Func<Expression<Func<T, bool>>, Task<bool>> checkAnyDuplicatedItemAsyncFunction)
    {
        return await checkAnyDuplicatedItemAsyncFunction(FindOtherDuplicatedItemExpr)
            ? Invalid(TargetItem, "", ErrorMessage)
            : Valid(TargetItem);
    }

    public PlatformValidationResult Validate(IQueryable<T> checkUniquenessScope)
    {
        return checkUniquenessScope.Any(FindOtherDuplicatedItemExpr)
            ? Invalid(TargetItem, "", ErrorMessage)
            : Valid(TargetItem);
    }
}
