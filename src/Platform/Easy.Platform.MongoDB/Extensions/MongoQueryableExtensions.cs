using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Easy.Platform.MongoDB.Extensions;

public static class MongoQueryableExtensions
{
    public static async Task<List<TSource>> ToListAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return await IAsyncCursorSourceExtensions.ToListAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static async Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.FirstOrDefaultAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static async Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.FirstAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static async Task<int> CountAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.CountAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static async Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.AnyAsync(source.As<IMongoQueryable<TSource>>(), cancellationToken);
    }

    public static async Task<int> CountAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.CountAsync(source.As<IMongoQueryable<TSource>>(), predicate, cancellationToken);
    }

    public static async Task<bool> AnyAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await MongoQueryable.AnyAsync(source.As<IMongoQueryable<TSource>>(), predicate, cancellationToken);
    }

    public static string ToQueryString<TSource>(
        this IMongoQueryable<TSource> source)
    {
        return $"[ Query:{source}; ElementType.Name:{source.ElementType.Name}; ]";
    }
}
