using System.Linq.Expressions;
using FluentValidation;

namespace Easy.Platform.Common.Validations.Validators;

public class PlatformSingleValidator<TTarget, TProperty> : PlatformValidator<TTarget>
{
    public PlatformSingleValidator(
        Expression<Func<TTarget, TProperty>> ruleForPropExpr,
        Action<IRuleBuilderInitial<TTarget, TProperty>> ruleBuilder)
    {
        RuleForPropExpr = ruleForPropExpr;
        ruleBuilder(RuleFor(RuleForPropExpr));
    }

    public Expression<Func<TTarget, TProperty>> RuleForPropExpr { get; }

    public static implicit operator PlatformSingleValidator<TTarget, TProperty>(
        ValueTuple<Expression<Func<TTarget, TProperty>>, Action<IRuleBuilderInitial<TTarget, TProperty>>> validatorInfo)
    {
        return new PlatformSingleValidator<TTarget, TProperty>(validatorInfo.Item1, validatorInfo.Item2);
    }
}
