using System.Linq.Expressions;

namespace Easy.Platform.Common.Extensions;

public static class ExpressionExtension
{
    /// <summary>
    /// Combines two expressions with the logical AND operator conditionally based on the provided boolean value.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="expression">The first expression to combine.</param>
    /// <param name="if">A boolean value determining whether to combine the expressions.</param>
    /// <param name="andExpression">The second expression func builder to combine if the condition is met.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static Expression<Func<T, bool>> AndAlsoIf<T>(
        this Expression<Func<T, bool>> expression,
        bool @if,
        Func<Expression<Func<T, bool>>> andExpression)
    {
        if (@if)
            return expression.AndAlso(andExpression());

        return expression;
    }

    /// <summary>
    /// Combines two expressions with the logical AND operator conditionally based on the provided boolean value. The second expression is obtained asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="expression">The first expression to combine.</param>
    /// <param name="if">A boolean value determining whether to combine the expressions.</param>
    /// <param name="andExprAsync">An asynchronous function providing the second expression.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static async Task<Expression<Func<T, bool>>> AndAlsoIf<T>(
        this Expression<Func<T, bool>> expression,
        bool @if,
        Func<Task<Expression<Func<T, bool>>>> andExprAsync)
    {
        if (@if)
            return expression.AndAlso(await andExprAsync());

        return expression;
    }


    /// <summary>
    /// Combines two expressions with the logical AND operator conditionally based on the provided boolean value. Both expressions are obtained asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="expressionTask">A task returning the first expression to combine.</param>
    /// <param name="if">A boolean value determining whether to combine the expressions.</param>
    /// <param name="andExprAsync">An asynchronous function providing the second expression.</param>
    /// <returns>A task returning a new expression representing the combined result.</returns>
    public static Task<Expression<Func<T, bool>>> AndAlsoIf<T>(
        this Task<Expression<Func<T, bool>>> expressionTask,
        bool @if,
        Func<Task<Expression<Func<T, bool>>>> andExprAsync)
    {
        if (@if)
            return expressionTask.Then(async expression => expression.AndAlso(await andExprAsync()));

        return expressionTask;
    }

    /// <summary>
    /// Combines two expressions with the logical AND operator conditionally based on the provided boolean value. Both expressions are obtained asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="expressionTask">A task returning the first expression to combine.</param>
    /// <param name="if">A boolean value determining whether to combine the expressions.</param>
    /// <param name="andExpression">The second expression func builder to combine if the condition is met.</param>
    /// <returns>A task returning a new expression representing the combined result.</returns>
    public static Task<Expression<Func<T, bool>>> AndAlsoIf<T>(
        this Task<Expression<Func<T, bool>>> expressionTask,
        bool @if,
        Func<Expression<Func<T, bool>>> andExpression)
    {
        if (@if)
            return expressionTask.Then(expression => expression.AndAlso(andExpression()));

        return expressionTask;
    }

    /// <summary>
    /// Combines two expressions with the logical OR operator conditionally based on the provided boolean value.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="expression">The first expression to combine.</param>
    /// <param name="if">A boolean value determining whether to combine the expressions.</param>
    /// <param name="andExpression">The second expression func builder to combine if the condition is met.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static Expression<Func<T, bool>> OrIf<T>(
        this Expression<Func<T, bool>> expression,
        bool @if,
        Func<Expression<Func<T, bool>>> andExpression)
    {
        if (@if)
            return expression.Or(andExpression());

        return expression;
    }

    /// <summary>
    /// Returns the name of the specified property of the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The type the property is a member of.
    /// </typeparam>
    /// <typeparam name="TProp">The type of the property.</typeparam>
    /// <param name="property">
    /// The property.
    /// </param>
    /// <returns>
    /// The property name.
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

    /// <summary>
    /// Combines two expressions with the logical AND operator.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="first">The first expression to combine.</param>
    /// <param name="second">The second expression to combine.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        if (first.IsConstantTrue()) return second;
        if (second.IsConstantTrue()) return first;

        return first.Compose(second, Expression.AndAlso);
    }

    /// <summary>
    /// Combines two expressions with the logical AND operator and negates the result.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="first">The first expression to combine.</param>
    /// <param name="second">The second expression to combine.</param>
    /// <returns>A new expression representing the combined result negated.</returns>
    public static Expression<Func<T, bool>> AndAlsoNot<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return AndAlso(first, second).Not();
    }

    /// <summary>
    /// Combines two expressions with the logical OR operator.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="first">The first expression to combine.</param>
    /// <param name="second">The second expression to combine.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second, Expression.OrElse);
    }

    /// <summary>
    /// Combines two expressions with the logical OR operator and negates the result.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="first">The first expression to combine.</param>
    /// <param name="second">The second expression to combine.</param>
    /// <returns>A new expression representing the combined result negated.</returns>
    public static Expression<Func<T, bool>> OrNot<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second.Not(), Expression.OrElse);
    }

    /// <summary>
    /// Negates the specified expression.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expression.</typeparam>
    /// <param name="one">The expression to negate.</param>
    /// <returns>A new expression representing the negated result.</returns>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> one)
    {
        var candidateExpr = one.Parameters[0];
        var body = Expression.Not(one.Body);

        return Expression.Lambda<Func<T, bool>>(body, candidateExpr);
    }

    /// <summary>
    /// Determines whether the body of the specified expression is a constant expression that evaluates to true.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expression.</typeparam>
    /// <typeparam name="TResult">The type of the result of the expression.</typeparam>
    /// <param name="expr">The expression to check.</param>
    /// <returns>
    /// true if the body of the expression is a constant expression that evaluates to true; otherwise, false.
    /// </returns>
    public static bool IsConstantTrue<T, TResult>(this Expression<Func<T, TResult>> expr)
    {
        return expr.Body.NodeType == ExpressionType.Constant && true.Equals(((ConstantExpression)expr.Body).Value);
    }

    /// <summary>
    /// Determines whether the body of the specified expression is a constant expression that evaluates to false.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expression.</typeparam>
    /// <typeparam name="TResult">The type of the result of the expression.</typeparam>
    /// <param name="expr">The expression to check.</param>
    /// <returns>
    /// true if the body of the expression is a constant expression that evaluates to false; otherwise, false.
    /// </returns>
    public static bool IsConstantFalse<T, TResult>(this Expression<Func<T, TResult>> expr)
    {
        return expr.Body.NodeType == ExpressionType.Constant && false.Equals(((ConstantExpression)expr.Body).Value);
    }


    /// <summary>
    /// Combines two expressions using the specified merge function.
    /// </summary>
    /// <typeparam name="T">The type of the parameter of the expressions.</typeparam>
    /// <param name="firstExpr">The first expression to combine.</param>
    /// <param name="secondExpr">The second expression to combine.</param>
    /// <param name="merge">The function used to merge the expressions.</param>
    /// <returns>A new expression representing the combined result.</returns>
    public static Expression<T> Compose<T>(this Expression<T> firstExpr, Expression<T> secondExpr, Func<Expression, Expression, Expression> merge)
    {
        // replace parameters in the second lambda expression with parameters from the first
        var secondExprBody = ParameterRebinder.ReplaceParameters(secondExpr, firstExpr);

        // apply composition of lambda expression bodies to parameters from the first expression
        return Expression.Lambda<T>(merge(firstExpr.Body, secondExprBody), firstExpr.Parameters);
    }
}
