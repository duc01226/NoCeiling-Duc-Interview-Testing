using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Extensions;

public static class EfCoreQueryableExtensions
{
    public static Task<List<TSource>> EfCoreToListAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.ToListAsync(cancellationToken);
    }

    public static Task<TSource> EfCoreFirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.FirstOrDefaultAsync(cancellationToken);
    }

    public static Task<TSource> EfCoreFirstOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return source.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public static Task<TSource> EfCoreFirstAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.FirstAsync(cancellationToken);
    }

    public static Task<int> EfCoreCountAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.CountAsync(cancellationToken);
    }

    public static Task<bool> EfCoreAnyAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return source.AnyAsync(cancellationToken);
    }

    public static Task<int> EfCoreCountAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return source.CountAsync(predicate, cancellationToken);
    }

    public static Task<bool> EfCoreAnyAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return source.AnyAsync(predicate, cancellationToken);
    }
}
