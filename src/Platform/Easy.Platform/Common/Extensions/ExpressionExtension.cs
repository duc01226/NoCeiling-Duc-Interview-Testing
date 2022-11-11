using System.Linq.Expressions;

namespace Easy.Platform.Common.Extensions;

public static class ExpressionExtension
{
    public static Expression<Func<T, bool>> AndAlsoIf<T>(
        this Expression<Func<T, bool>> expression,
        bool @if,
        Expression<Func<T, bool>> andExpression)
    {
        if (@if)
            return expression.AndAlso(andExpression);

        return expression;
    }

    public static Expression<Func<T, bool>> OrIf<T>(
        this Expression<Func<T, bool>> expression,
        bool @if,
        Expression<Func<T, bool>> andExpression)
    {
        if (@if)
            return expression.Or(andExpression);

        return expression;
    }

    /// <summary>
    ///     Returns the name of the specified property of the specified type.
    /// </summary>
    /// <typeparam name="T">
    ///     The type the property is a member of.
    /// </typeparam>
    /// <typeparam name="TProp">The type of the property.</typeparam>
    /// <param name="property">
    ///     The property.
    /// </param>
    /// <returns>
    ///     The property name.
    /// </returns>
    public static string GetPropertyName<T, TProp>(this Expression<Func<T, TProp>> property)
    {
        LambdaExpression lambda = property;

        return lambda.Body switch
        {
            UnaryExpression unaryExpression => ((MemberExpression)unaryExpression.Operand).Member.Name,
            ConstantExpression constantExpression => constantExpression.ToString(),
            _ => ((MemberExpression)lambda.Body).Member.Name
        };
    }


    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second, Expression.AndAlso);
    }

    public static Expression<Func<T, bool>> AndAlsoNot<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second.Not(), Expression.AndAlso);
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second, Expression.OrElse);
    }

    public static Expression<Func<T, bool>> OrNot<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second.Not(), Expression.OrElse);
    }

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> one)
    {
        var candidateExpr = one.Parameters[0];
        var body = Expression.Not(one.Body);

        return Expression.Lambda<Func<T, bool>>(body, candidateExpr);
    }
}
