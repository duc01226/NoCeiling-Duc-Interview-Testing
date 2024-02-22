using System.Linq.Expressions;

namespace Easy.Platform.Common.Extensions;

public static class QueryableExtension
{
    /// <summary>
    /// Applies pagination to the provided query.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="query">The IQueryable[T] to apply pagination to.</param>
    /// <param name="skipCount">The number of elements to skip before returning the remaining elements.</param>
    /// <param name="maxResultCount">The maximum number of elements to return.</param>
    /// <returns>A new IQueryable[T] that has pagination applied.</returns>
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int? skipCount, int? maxResultCount)
    {
        return query
            .PipeIf(skipCount >= 0, _ => _.Skip(skipCount!.Value))
            .PipeIf(maxResultCount >= 0, _ => _.Take(maxResultCount!.Value));
    }

    /// <summary>
    /// Filters a sequence of values based on a predicate if the condition is true.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="query">An <see cref="IQueryable{T}" /> to filter.</param>
    /// <param name="if">A boolean value representing the condition.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>An <see cref="IQueryable{T}" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool @if, Expression<Func<T, bool>> predicate)
    {
        return @if
            ? query.Where(predicate)
            : query;
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool @if, Func<Expression<Func<T, bool>>> predicateBuilder)
    {
        return @if
            ? query.Where(predicateBuilder())
            : query;
    }

    /// <summary>
    /// Orders the elements of a sequence in ascending or descending order according to a key.
    /// </summary>
    /// <typeparam name="T">The type of the elements of <paramref name="query" />.</typeparam>
    /// <param name="query">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="orderDirection">The direction of the order (ascending or descending).</param>
    /// <returns>An <see cref="IOrderedQueryable{T}" /> whose elements are sorted according to a key.</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, Expression<Func<T, object>> keySelector, QueryOrderDirection orderDirection)
    {
        return orderDirection == QueryOrderDirection.Desc
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
    }

    /// <summary>
    /// Orders the elements of a sequence in ascending or descending order according to a property name.
    /// </summary>
    /// <typeparam name="T">The type of the elements of <paramref name="query" />.</typeparam>
    /// <param name="query">A sequence of values to order.</param>
    /// <param name="propertyName">The name of the property to order the elements by.</param>
    /// <param name="orderDirection">The direction of the order (ascending or descending).</param>
    /// <returns>An <see cref="IOrderedQueryable{T}" /> whose elements are sorted according to a property.</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, string propertyName, QueryOrderDirection orderDirection = QueryOrderDirection.Asc)
    {
        return orderDirection == QueryOrderDirection.Desc
            ? query.OrderByDescending(GetSortExpression<T>(propertyName))
            : query.OrderBy(GetSortExpression<T>(propertyName));
    }

    /// <summary>
    /// Generates a sorting expression based on the property name for the given type.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="propertyName">The name of the property to sort by.</param>
    /// <returns>An expression that represents the sorting operation for the specified property.</returns>
    public static Expression<Func<T, object>> GetSortExpression<T>(string propertyName)
    {
        var item = Expression.Parameter(typeof(T));
        var prop = Expression.Convert(Expression.Property(item, propertyName), typeof(object));
        var selector = Expression.Lambda<Func<T, object>>(prop, item);

        return selector;
    }
}

public enum QueryOrderDirection
{
    Asc,
    Desc
}

internal sealed class ParameterRebinder : ExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, ParameterExpression> targetToSourceParamsMap;

    public ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> targetToSourceParamsMap)
    {
        this.targetToSourceParamsMap = targetToSourceParamsMap ?? [];
    }

    // replace parameters in the target lambda expression with parameters from the source
    public static Expression ReplaceParameters<T>(Expression<T> targetExpr, Expression<T> sourceExpr)
    {
        var currentTargetToSourceParamsMap = sourceExpr.Parameters
            .Select(
                (sourceParam, firstParamIndex) => new
                {
                    sourceParam,
                    targetParam = targetExpr.Parameters[firstParamIndex]
                })
            .ToDictionary(p => p.targetParam, p => p.sourceParam);

        return new ParameterRebinder(currentTargetToSourceParamsMap).Visit(targetExpr.Body);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (targetToSourceParamsMap.TryGetValue(node, out var replacement))
            node = replacement;

        return base.VisitParameter(node);
    }
}
