using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Easy.Platform.EfCore.EntityConfiguration.ValueComparers;

public class ToJsonValueComparer<T> : ValueComparer<T>
{
    protected ToJsonValueComparer(bool favorStructuralComparisons) : base(favorStructuralComparisons)
    {
    }

    protected ToJsonValueComparer(Expression<Func<T, T, bool>> equalsExpression, Expression<Func<T, int>> hashCodeExpression) : base(equalsExpression, hashCodeExpression)
    {
    }

    protected ToJsonValueComparer(Expression<Func<T, T, bool>> equalsExpression, Expression<Func<T, int>> hashCodeExpression, Expression<Func<T, T>> snapshotExpression) : base(
        equalsExpression,
        hashCodeExpression,
        snapshotExpression)
    {
    }

    public ToJsonValueComparer() : base((c1, c2) => c1.IsValuesEqual(c2), c => c.GetHashCode())
    {
    }
}
