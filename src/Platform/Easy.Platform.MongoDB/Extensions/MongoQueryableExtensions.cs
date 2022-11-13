using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Extensions;

public static class MongoQueryableExtensions
{
    public static Task<List<TSource>> ToListAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return IAsyncCursorSourceExtensions.ToListAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return MongoQueryable.FirstOrDefaultAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return MongoQueryable.FirstAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static Task<int> CountAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return MongoQueryable.CountAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return MongoQueryable.AnyAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static Task<int> CountAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return MongoQueryable.CountAsync(source.As<IMongoQueryable<TSource>>(), predicate, cancellationToken);
    }

    public static Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return MongoQueryable.AnyAsync(source.As<IMongoQueryable<TSource>>(), predicate, cancellationToken);
    }
}
