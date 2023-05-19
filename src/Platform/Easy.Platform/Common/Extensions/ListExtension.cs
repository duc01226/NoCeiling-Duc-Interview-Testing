#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Easy.Platform.Common.Extensions;

public static class ListExtension
{
    public static List<T> RemoveWhere<T>(this IList<T> items, Func<T, bool> predicate, out List<T> removedItems)
    {
        var toRemoveItems = new List<T>();

        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                toRemoveItems.Add(items[i]);
                items.RemoveAt(i);
                i--;
            }
        }

        removedItems = toRemoveItems;

        return items.ToList();
    }

    /// <summary>
    /// Remove item in this and return removed items
    /// </summary>
    public static List<T> RemoveMany<T>(this IList<T> items, IList<T> toRemoveItems) where T : notnull
    {
        var toRemoveItemsDic = toRemoveItems.ToDictionary(p => p);

        var removedItems = new List<T>();

        for (var i = 0; i < items.Count; i++)
        {
            if (toRemoveItemsDic.ContainsKey(items[i]))
            {
                removedItems.Add(items[i]);
                items.RemoveAt(i);
                i--;
            }
        }

        return removedItems;
    }

    public static T? RemoveFirst<T>(this IList<T> items, Func<T, bool> predicate)
    {
        var toRemoveItem = items.FirstOrDefault(predicate);

        if (toRemoveItem != null) items.Remove(toRemoveItem);

        return toRemoveItem;
    }

    /// <summary>
    /// Remove last item in list and return it
    /// </summary>
    public static T Pop<T>(this IList<T> items)
    {
        var lastItemIndex = items.Count - 1;

        var toRemoveItem = items[lastItemIndex];

        items.RemoveAt(lastItemIndex);

        return toRemoveItem;
    }

    public static void UpdateWhere<T>(this IList<T> items, Func<T, bool> predicate, Action<T> updateAction)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
                updateAction(items[i]);
        }
    }

    public static void UpsertWhere<T>(this IList<T> items, Func<T, bool> predicate, T item)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                items[i] = item;
                return;
            }
        }

        items.Add(item);
    }

    public static IEnumerable<T> ConcatSingle<T>(this IEnumerable<T> items, T item)
    {
        return items.Concat(
            new List<T>
            {
                item
            });
    }

    public static bool IsEmpty<T>(this IEnumerable<T> items)
    {
        return !items.Any();
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? items)
    {
        return items == null || !items.Any();
    }

    public static bool NotExist<T>(this IEnumerable<T> items, Func<T, bool> predicate)
    {
        return !items.Any(predicate);
    }

    public static bool NotContains<T>(this IEnumerable<T> items, T item)
    {
        return !items.Contains(item);
    }

    public static List<T> AddDistinct<T>(this IList<T> items, T item)
    {
        if (!items.Contains(item)) items.Add(item);

        return items.ToList();
    }

    public static List<T> WhereIf<T>(this IEnumerable<T> items, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition
            ? items.Where(predicate.Compile()).ToList()
            : items.ToList();
    }

    public static bool ContainsAll<T>(this IList<T> items, IList<T> containAllItems)
    {
        return items.Intersect(containAllItems).Count() >= containAllItems.Count;
    }

    public static bool ItemsMatch<T, T1>(this IList<T> items, IList<T1> matchedWithItems, Func<T, T1, bool> mustMatch)
    {
        if (items.Count != matchedWithItems.Count) return false;

        for (var i = 0; i < items.Count; i++)
        {
            if (mustMatch(items[i], matchedWithItems[i]) == false)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Example: list.ForEach((item, itemIndex) => do some thing)
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> items, Action<T, int> action)
    {
        var itemsList = items.As<IList<T>>() ?? items.ToList();

        for (var i = 0; i < itemsList.Count; i++) action(itemsList[i], i);
    }

    /// <summary>
    /// Example: list.ForEach((item, itemIndex) => do some thing)
    /// </summary>
    public static void ForEach<T>(this IList<T> items, Action<T, int> action)
    {
        for (var i = 0; i < items.Count; i++) action(items[i], i);
    }

    /// <summary>
    /// Example: await list.ForEach((item, itemIndex) => do some thing async)
    /// </summary>
    public static async Task ForEachAsync<T>(this IEnumerable<T> items, Func<T, int, Task> action)
    {
        var itemsList = items.As<IList<T>>() ?? items.ToList();

        for (var i = 0; i < itemsList.Count; i++) await action(itemsList[i], i);
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static async Task ForEachAsync<T, TActionResult>(this IEnumerable<T> items, Func<T, int, Task<TActionResult>> action)
    {
        var itemsList = items.As<IList<T>>() ?? items.ToList();

        for (var i = 0; i < itemsList.Count; i++) await action(itemsList[i], i);
    }

    /// <summary>
    /// Example: await list.ForEach((item, itemIndex) => do some thing async)
    /// </summary>
    public static async Task ForEachAsync<T>(this IList<T> items, Func<T, int, Task> action)
    {
        for (var i = 0; i < items.Count; i++) await action(items[i], i);
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static async Task ForEachAsync<T, TActionResult>(this IList<T> items, Func<T, int, Task<TActionResult>> action)
    {
        for (var i = 0; i < items.Count; i++) await action(items[i], i);
    }

    /// <inheritdoc cref="ForEach{T}(IEnumerable{T},Action{T,int})" />
    public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        items.ForEach((item, index) => action(item));
    }

    /// <inheritdoc cref="ForEach{T}(IEnumerable{T},Action{T,int})" />
    public static void ForEach<T>(this IList<T> items, Action<T> action)
    {
        items.ForEach((item, index) => action(item));
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static Task ForEachAsync<T>(this IEnumerable<T> items, Func<T, Task> action)
    {
        return items.ForEachAsync((item, index) => action(item));
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static Task ForEachAsync<T, TActionResult>(this IEnumerable<T> items, Func<T, Task<TActionResult>> action)
    {
        return items.ForEachAsync((item, index) => action(item));
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static Task ForEachAsync<T>(this IList<T> items, Func<T, Task> action)
    {
        return items.ForEachAsync((item, index) => action(item));
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static Task ForEachAsync<T, TActionResult>(this IList<T> items, Func<T, Task<TActionResult>> action)
    {
        return items.ForEachAsync((item, index) => action(item));
    }

    /// <summary>
    /// Example: var listB = await list.SelectAsync((item, itemIndex) => get B async)
    /// </summary>
    public static async Task<List<TActionResult>> SelectAsync<T, TActionResult>(
        this IEnumerable<T> items,
        Func<T, int, Task<TActionResult>> actionAsync)
    {
        var result = new List<TActionResult>();

        var itemsList = items.As<IList<T>>() ?? items.ToList();

        for (var i = 0; i < itemsList.Count; i++) result.Add(await actionAsync(itemsList[i], i));

        return result;
    }

    /// <inheritdoc cref="SelectAsync{T,TResult}(IEnumerable{T},Func{T,int,Task{TResult}})" />
    public static Task<List<TActionResult>> SelectAsync<T, TActionResult>(
        this IEnumerable<T> items,
        Func<T, Task<TActionResult>> actionAsync)
    {
        return items.SelectAsync((item, index) => actionAsync(item));
    }

    /// <summary>
    /// Example: var listB = await list.SelectAsync((item, itemIndex) => get B async)
    /// </summary>
    public static async Task<List<TActionResult>> SelectAsync<T, TActionResult>(
        this IList<T> items,
        Func<T, int, Task<TActionResult>> actionAsync)
    {
        var result = new List<TActionResult>();

        for (var i = 0; i < items.Count; i++) result.Add(await actionAsync(items[i], i));

        return result;
    }

    /// <inheritdoc cref="SelectAsync{T,TResult}(IEnumerable{T},Func{T,int,Task{TResult}})" />
    public static Task<List<TActionResult>> SelectAsync<T, TActionResult>(
        this IList<T> items,
        Func<T, Task<TActionResult>> actionAsync)
    {
        return items.SelectAsync((item, index) => actionAsync(item));
    }

    /// <summary>
    /// Example: var listB = await Task{list}.ThenSelect((item, itemIndex) => get B)
    /// </summary>
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<IEnumerable<T>> itemsTask,
        Func<T, int, TActionResult> selector)
    {
        return itemsTask.Then(items => items.Select(selector).ToList());
    }

    /// <inheritdoc cref="ThenSelect{T,TResult}(Task{IEnumerable{T}},Func{T,int,Task{TResult}})" />
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<IEnumerable<T>> itemsTask,
        Func<T, TActionResult> selector)
    {
        return itemsTask.ThenSelect((item, index) => selector(item));
    }

    /// <inheritdoc cref="ThenSelect{T,TResult}(Task{IEnumerable{T}},Func{T,int,Task{TResult}})" />
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, int, TActionResult> selector)
    {
        return itemsTask.Then(items => items.Select(selector).ToList());
    }

    /// <inheritdoc cref="ThenSelect{T,TResult}(Task{IEnumerable{T}},Func{T,int,Task{TResult}})" />
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, TActionResult> selector)
    {
        return itemsTask.ThenSelect((item, index) => selector(item));
    }

    public static List<T1> Map<T, T1>(this IList<T> items, Func<T, T1> mapFunc)
    {
        return items.Select(mapFunc).ToList();
    }

    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
    {
        return items.SelectMany(p => p);
    }

    public static ValueTuple<Dictionary<TKey, T>, List<TKey>> ToDictionaryWithKeysList<T, TKey>(this IEnumerable<T> items, Func<T, TKey> selectKey) where TKey : notnull
    {
        var dict = items.ToDictionary(selectKey, p => p);
        var keys = dict.Keys.ToList();

        return (dict, keys);
    }

    public static List<TResult> SelectList<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        return source.Select(selector).ToList();
    }

    public static HashSet<TResult> SelectHashset<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        return source.Select(selector).ToHashSet();
    }

    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, bool @if, IEnumerable<TSource> second)
    {
        return @if ? source.Concat(second) : source;
    }

    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, bool @if, params TSource[] second)
    {
        return ConcatIf(source, @if, (IEnumerable<TSource>)second);
    }

    public static IEnumerable<TSource> ConcatIf<TSource>(
        this IEnumerable<TSource> source,
        Func<IEnumerable<TSource>, bool> @if,
        Func<IEnumerable<TSource>, IEnumerable<TSource>> second)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        return @if(sourceList) ? sourceList.Concat(second(sourceList)) : sourceList;
    }

    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, Func<IEnumerable<TSource>, bool> @if, params TSource[] second)
    {
        return ConcatIf(source, @if, p => second);
    }

    public static List<T> Exclude<T>(this IList<T> items, IList<T> excludeItems)
    {
        var excludeItemsHashSet = excludeItems.ToHashSet();
        return items
            .Where(p => !excludeItemsHashSet.Contains(p))
            .ToList();
    }

    public static string? JoinToString<T>([NotNullIfNotNull(nameof(items))] this IEnumerable<T>? items, string separator = "") where T : notnull
    {
        return items != null ? string.Join(separator, items.Select(p => p.ToString())) : null;
    }

    public static string? JoinToString<T>(this IEnumerable<T> items, char separator) where T : notnull
    {
        return JoinToString(items, separator.ToString());
    }

    // Add this to fix ambiguous evocation with other library by help compiler to select exact type extension
    public static bool IsNullOrEmpty<T>(this List<T>? items)
    {
        return items == null || !items.Any();
    }

    // Add this to fix ambiguous evocation with other library by help compiler to select exact type extension
    public static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue>? items) where TKey : notnull
    {
        return items == null || !items.Any();
    }

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }

    public static IEnumerable<TResult> SelectManyNullable<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, IEnumerable<TResult>?> selector)
    {
        return source.SelectMany(p => selector(p) ?? new List<TResult>());
    }

    public static async IAsyncEnumerable<TResult> SelectManyAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, IAsyncEnumerable<TResult>> selector)
    {
        foreach (var i in source)
        {
            await foreach (var item in selector(i)) yield return item;
        }
    }

    public static IEnumerable<T> PageBy<T>(this IEnumerable<T> query, int? skipCount, int? maxResultCount)
    {
        return query
            .PipeIf(skipCount >= 0, _ => _.Skip(skipCount!.Value))
            .PipeIf(maxResultCount >= 0, _ => _.Take(maxResultCount!.Value));
    }

    public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        for (var i = 0; i < sourceList.Count; i++)
        {
            if (!predicate(sourceList[i], i))
                return false;
        }

        return true;
    }

    public static TSource? FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        for (var i = 0; i < sourceList.Count; i++)
        {
            if (predicate(sourceList[i], i))
                return sourceList[i];
        }

        return default;
    }

    public static TSource First<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        for (var i = 0; i < sourceList.Count; i++)
        {
            if (predicate(sourceList[i], i))
                return sourceList[i];
        }

        throw new Exception("Item not found");
    }

    public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        for (var i = 0; i < sourceList.Count; i++)
        {
            if (predicate(sourceList[i], i))
                return true;
        }

        return false;
    }
}
