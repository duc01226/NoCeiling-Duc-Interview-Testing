using System.Linq.Expressions;
using FluentValidation;

namespace Easy.Platform.Common.Validations.Validators;

public class PlatformExpressionValidator<T> : PlatformValidator<T>
{
    public PlatformExpressionValidator(Expression<Func<T, bool>> must, string errorMessage)
    {
        ValidExpr = must;
        ErrorMessage = errorMessage;

        RuleFor(x => x).Must(must.Compile()).WithMessage(errorMessage);
    }

    public Expression<Func<T, bool>> ValidExpr { get; }
    public string ErrorMessage { get; }
}
