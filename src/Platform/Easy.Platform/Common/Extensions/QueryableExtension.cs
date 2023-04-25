using System.Linq.Expressions;

namespace Easy.Platform.Common.Extensions;

public static class QueryableExtension
{
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int? skipCount, int? maxResultCount)
    {
        return query
            .PipeIf(skipCount >= 0, _ => _.Skip(skipCount!.Value))
            .PipeIf(maxResultCount >= 0, _ => _.Take(maxResultCount!.Value));
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool @if, Expression<Func<T, bool>> predicate)
    {
        return @if
            ? query.Where(predicate)
            : query;
    }

    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, Expression<Func<T, object>> keySelector, QueryOrderDirection orderDirection)
    {
        return orderDirection == QueryOrderDirection.Desc
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
    }

    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, string propertyName, QueryOrderDirection orderDirection = QueryOrderDirection.Asc)
    {
        return orderDirection == QueryOrderDirection.Desc
            ? query.OrderByDescending(GetSortExpression<T>(propertyName))
            : query.OrderBy(GetSortExpression<T>(propertyName));
    }

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
        this.targetToSourceParamsMap = targetToSourceParamsMap ?? new Dictionary<ParameterExpression, ParameterExpression>();
    }

    // replace parameters in the target lambda expression with parameters from the source
    public static Expression ReplaceParameters<T>(Expression<T> targetExpr, Expression<T> sourceExpr)
    {
        var targetToSourceParamsMap = sourceExpr.Parameters
            .Select(
                (sourceParam, firstParamIndex) => new
                {
                    sourceParam,
                    targetParam = targetExpr.Parameters[firstParamIndex]
                })
            .ToDictionary(p => p.targetParam, p => p.sourceParam);

        return new ParameterRebinder(targetToSourceParamsMap).Visit(targetExpr.Body);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (targetToSourceParamsMap.TryGetValue(node, out var replacement))
            node = replacement;

        return base.VisitParameter(node);
    }
}
