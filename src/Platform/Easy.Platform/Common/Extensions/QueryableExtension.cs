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

    public static Expression<T> Compose<T>(this Expression<T> firstExpr, Expression<T> secondExpr, Func<Expression, Expression, Expression> merge)
    {
        // replace parameters in the second lambda expression with parameters from the first
        var secondExprBody = ParameterRebinder.ReplaceParameters(secondExpr, firstExpr);

        // apply composition of lambda expression bodies to parameters from the first expression
        return Expression.Lambda<T>(merge(firstExpr.Body, secondExprBody), firstExpr.Parameters);
    }
}

internal class ParameterRebinder : ExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, ParameterExpression> targetToSourceParamsMap;

    public ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> targetToSourceParamsMap)
    {
        this.targetToSourceParamsMap = targetToSourceParamsMap ?? new Dictionary<ParameterExpression, ParameterExpression>();
    }

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
