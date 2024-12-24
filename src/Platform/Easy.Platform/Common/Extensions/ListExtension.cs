#nullable enable
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class ListExtension
{
    /// <summary>
    /// Removes all elements from the list that satisfy the provided predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="items">The list from which to remove elements.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="removedItems">When this method returns, contains the list of elements that were removed from the list.</param>
    /// <returns>The list after removing the elements that satisfy the predicate.</returns>
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
    /// Removes all elements from the list that match the conditions defined by the specified predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the list.</typeparam>
    /// <param name="items">The list from which to remove elements.</param>
    /// <param name="predicate">The delegate that defines the conditions of the elements to remove.</param>
    /// <returns>A list which contains the remaining elements after the operation.</returns>
    public static List<T> RemoveWhere<T>(this IList<T> items, Func<T, bool> predicate)
    {
        return RemoveWhere(items, predicate, out _);
    }

    /// <summary>
    /// Removes multiple items from the given list.
    /// </summary>
    /// <param name="items">The list from which items will be removed.</param>
    /// <param name="toRemoveItems">The items to be removed from the list.</param>
    /// <returns>A list of items that were successfully removed.</returns>
    /// <typeparam name="T">The type of items in the list. Must be a non-null type.</typeparam>
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

    /// <summary>
    /// Removes the first item from the list that matches the provided predicate.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    /// <param name="items">The list from which to remove the item.</param>
    /// <param name="predicate">A function to test each item for a condition.</param>
    /// <returns>The removed item if the list contains an item that matches the predicate; otherwise, null.</returns>
    public static T? RemoveFirst<T>(this IList<T> items, Func<T, bool> predicate)
    {
        var toRemoveItem = items.FirstOrDefault(predicate);

        if (toRemoveItem is not null) items.Remove(toRemoveItem);

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

    /// <summary>
    /// Updates elements in a list based on a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="items">The list of items to update.</param>
    /// <param name="predicate">The condition that determines which items to update.</param>
    /// <param name="updateAction">The action to perform on each item that satisfies the condition.</param>
    /// <remarks>
    /// This method iterates through the list and applies the update action to each item that satisfies the specified condition.
    /// </remarks>
    public static void UpdateWhere<T>(this IList<T> items, Func<T, bool> predicate, Action<T> updateAction)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
                updateAction(items[i]);
        }
    }

    /// <summary>
    /// Updates an item in the list if it satisfies the provided predicate, or adds the item to the list if no such item exists.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    /// <param name="items">The list of items.</param>
    /// <param name="predicate">The function to test each item for a condition.</param>
    /// <param name="item">The item to update or insert.</param>
    /// <remarks>
    /// This method performs an 'upsert' operation (update or insert) on the list. It iterates over the list and replaces the first item that satisfies the provided predicate with the given item. If no such item exists in the list, the method adds the given item to the list.
    /// </remarks>
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

    /// <summary>
    /// Upserts (insert or update) items in the list based on a specified key function and a collection of items to upsert.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="items">The list of items to upsert.</param>
    /// <param name="upsertByFn">The function to extract a key from each item for upsert comparison.</param>
    /// <param name="upsertItems">The collection of items to upsert into the list.</param>
    /// <param name="updateFn">Optional. The function to perform an update when an item with the same key already exists in the list.</param>
    /// <remarks>
    /// This method iterates through the provided collection of items to upsert. For each item, it uses the specified key function to determine if an item with the same key already exists in the list.
    /// If found, it either replaces the existing item or performs a custom update based on the provided update function. If not found, the item is added to the list.
    /// </remarks>
    public static void UpsertBy<T>(this IList<T> items, Func<T, object?> upsertByFn, IList<T> upsertItems, Func<T, T, T>? updateFn = null)
    {
        var groupedByUpsertValueItems =
            items.Select((item, index) => new { item, index }).GroupBy(p => upsertByFn(p.item) ?? "").ToDictionary(p => p.Key, p => p.First());

        foreach (var upsertItem in upsertItems)
        {
            var upsertItemByValue = upsertByFn(upsertItem) ?? "";

            if (groupedByUpsertValueItems.TryGetValue(upsertItemByValue, out var toUpdateItemInfo))
            {
                if (updateFn == null) items[toUpdateItemInfo.index] = upsertItem;
                else items[toUpdateItemInfo.index] = updateFn(items[toUpdateItemInfo.index], upsertItem);
            }
            else
                items.Add(upsertItem);
        }
    }

    /// <summary>
    /// Concatenates a single item to the end of an existing IEnumerable of the same type.
    /// </summary>
    /// <typeparam name="T">The type of the items in the IEnumerable.</typeparam>
    /// <param name="items">The existing IEnumerable to which the item will be concatenated.</param>
    /// <param name="item">The single item to concatenate to the IEnumerable.</param>
    /// <returns>A new IEnumerable that includes the original items and the concatenated item.</returns>
    public static IEnumerable<T> ConcatSingle<T>(this IEnumerable<T> items, T item)
    {
        return items.Concat([item]);
    }

    /// <summary>
    /// Concatenates a single item to the end of an existing List of the same type.
    /// </summary>
    /// <typeparam name="T">The type of the items in the List.</typeparam>
    /// <param name="items">The existing List to which the item will be concatenated.</param>
    /// <param name="item">The single item to concatenate to the List.</param>
    /// <returns>A new List that includes the original items and the concatenated item.</returns>
    public static List<T> ConcatSingle<T>(this List<T> items, T item)
    {
        return items.Concat([item]).ToList();
    }

    /// <summary>
    /// Determines whether the specified collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="items">The collection to check for emptiness.</param>
    /// <returns>true if the collection is empty; otherwise, false.</returns>
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

    /// <summary>
    /// Adds an item to the list if it is not already present, considering distinctness based on a specified key selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="items">The list of items to add to.</param>
    /// <param name="item">The item to add to the list.</param>
    /// <param name="distinctByKeySelector">Optional. A function to determine the distinctness based on a specific key. If not provided, the default equality comparison will be used.</param>
    /// <returns>A new list containing the added item.</returns>
    /// <remarks>
    /// This method checks whether the item is already present in the list. If a key selector is provided, it checks distinctness based on that key. If not, it uses the default equality comparison.
    /// </remarks>
    public static List<T> AddDistinct<T>(this IList<T> items, T item, Func<T, object?>? distinctByKeySelector = null)
    {
        if ((distinctByKeySelector == null && !items.Contains(item)) ||
            (distinctByKeySelector != null && !items.Select(distinctByKeySelector).Contains(distinctByKeySelector(item)))) items.Add(item);

        return items.ToList();
    }

    /// <summary>
    /// Adds an item to the list if it is not already present, considering distinctness based on a specified key selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="items">The list of items to add to.</param>
    /// <param name="addItems">The items to add to the list.</param>
    /// <param name="distinctByKeySelector">Optional. A function to determine the distinctness based on a specific key. If not provided, the default equality comparison will be used.</param>
    /// <returns>A new list containing the added item.</returns>
    /// <remarks>
    /// This method checks whether the item is already present in the list. If a key selector is provided, it checks distinctness based on that key. If not, it uses the default equality comparison.
    /// </remarks>
    public static List<T> AddDistinct<T>(this IList<T> items, IList<T> addItems, Func<T, object?>? distinctByKeySelector = null)
    {
        var distinctByItemKeys = distinctByKeySelector != null ? items.Select(p => distinctByKeySelector(p)).ToHashSet() : null;

        addItems.ForEach(
            addItem =>
            {
                if ((distinctByItemKeys != null && !distinctByItemKeys.Contains(distinctByKeySelector!(addItem))) ||
                    (distinctByItemKeys == null && !items.Contains(addItem))) items.Add(addItem);
            });

        return items.ToList();
    }

    public static List<T> WhereIf<T>(this IEnumerable<T> items, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition
            ? items.Where(predicate.Compile()).ToList()
            : items.ToList();
    }

    public static bool ContainsAll<T>(this IEnumerable<T> items, IList<T> containAllItems)
    {
        return items.Intersect(containAllItems).Count() >= containAllItems.Count;
    }

    public static bool ContainsAny<T>(this IEnumerable<T> items, IList<T> containAllItems)
    {
        return items.Intersect(containAllItems).Any();
    }

    public static bool ContainsAny<T>(this IEnumerable<T> items, params T[] containAllItems)
    {
        return items.ContainsAny(containAllItems.ToList());
    }

    /// <summary>
    /// Determines whether all items in the given list match the corresponding items in another list based on a provided matching function.
    /// </summary>
    /// <typeparam name="T">The type of items in the first list.</typeparam>
    /// <typeparam name="T1">The type of items in the second list.</typeparam>
    /// <param name="items">The first list to compare.</param>
    /// <param name="matchedWithItems">The second list to compare.</param>
    /// <param name="mustMatch">A function to determine if two items match. The function takes an item from each list and returns a boolean indicating whether they match.</param>
    /// <returns>Returns true if all items in the first list match the corresponding items in the second list according to the provided matching function. Returns false if the lists are of different lengths or if any pair of items does not match.</returns>
    public static bool IsAllItemsMatch<T, T1>(this IList<T> items, IList<T1> matchedWithItems, Func<T, T1, bool> mustMatch)
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
    /// Executes a specified action on each item in the provided collection, along with the index of the item in the collection.
    /// </summary>
    /// <typeparam name="T">The type of the items in the collection.</typeparam>
    /// <param name="items">The collection on which to perform the action.</param>
    /// <param name="action">The action to perform on each item in the collection. This action includes the item itself and its index in the collection.</param>
    /// <example>
    /// This sample shows how to call the ForEach method.
    /// <code>
    /// list.ForEach((item, itemIndex) => Console.WriteLine($"Item: {item}, Index: {itemIndex}"));
    /// </code>
    /// </example>
    public static void ForEach<T>(this IEnumerable<T> items, Action<T, int> action)
    {
        if (items is List<T> itemsList)
        {
            var itemsSpan = CollectionsMarshal.AsSpan(itemsList);

            for (var i = 0; i < itemsSpan.Length; i++) action(itemsSpan[i], i);
        }
        else if (items is T[] itemsArray)
        {
            for (var i = 0; i < itemsArray.Length; i++)
                action(itemsArray[i], i);
        }
        else
        {
            var itemsIList = items.As<IList<T>>() ?? items.ToList();

            for (var i = 0; i < itemsIList.Count; i++) action(itemsIList[i], i);
        }
    }

    public static void ForEach<T>(this IEnumerable<T> items, Action action)
    {
        foreach (var _ in items) action();
    }

    /// <summary>
    /// Executes a specified action on each item in the provided list, along with the index of the item in the list.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    /// <param name="items">The list on which to perform the action.</param>
    /// <param name="action">The action to perform on each item in the list. This action includes the item itself and its index in the list.</param>
    /// <example>
    /// This sample shows how to call the ForEach method.
    /// <code>
    /// list.ForEach((item, itemIndex) => Console.WriteLine($"Item: {item}, Index: {itemIndex}"));
    /// </code>
    /// </example>
    public static void ForEach<T>(this IList<T> items, Action<T, int> action)
    {
        if (items is List<T> itemsList)
        {
            var itemsSpan = CollectionsMarshal.AsSpan(itemsList);

            for (var i = 0; i < itemsSpan.Length; i++) action(itemsSpan[i], i);
        }
        else
        {
            for (var i = 0; i < items.Count; i++)
                action(items[i], i);
        }
    }

    /// <summary>
    /// Asynchronously performs the specified action on each element of the <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="items">The <see cref="IEnumerable{T}" /> on which to perform the action.</param>
    /// <param name="action">The delegate to perform on each element of the <see cref="IEnumerable{T}" />.</param>
    /// <returns>A <see cref="Task" /> that represents the asynchronous operation.</returns>
    /// <example>
    /// This sample shows how to call the ForEachAsync method.
    /// <code>
    /// await list.ForEachAsync((item, itemIndex) => do something async)
    /// </code>
    /// </example>
    public static async Task ForEachAsync<T>(this IEnumerable<T> items, Func<T, int, Task> action)
    {
        if (items is T[] itemsArray)
        {
            for (var i = 0; i < itemsArray.Length; i++)
                await action(itemsArray[i], i);
        }
        else
        {
            var itemsIList = items.As<IList<T>>() ?? items.ToList();

            for (var i = 0; i < itemsIList.Count; i++) await action(itemsIList[i], i);
        }
    }

    public static async Task ForEachAsync<T>(this IEnumerable<T> items, Func<Task> action)
    {
        foreach (var _ in items) await action();
    }

    /// <summary>
    /// Executes an asynchronous action on each item of the provided collection in parallel, with a maximum number of concurrent operations.
    /// </summary>
    /// <param name="items">The collection of items to perform the action on.</param>
    /// <param name="action">The asynchronous action to perform on each item. The action takes two parameters: the item and its index in the collection.</param>
    /// <param name="maxConcurrent">The maximum number of concurrent operations. Defaults to <see cref="Util.TaskRunner.DefaultNumberOfParallelIoTasksPerCpuRatio" />.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method uses the <see cref="Parallel" /> class to execute the action in parallel.
    /// The order in which the action is applied to each item is not guaranteed.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the items collection or the action is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxConcurrent is less than 1.</exception>
    public static async Task ParallelAsync<T>(
        this IEnumerable<T> items,
        Func<T, int, Task> action,
        int? maxConcurrent = null)
    {
        await Util.TaskRunner.ParallelAsync(items, action, maxConcurrent);
    }

    /// <inheritdoc cref="Util.TaskRunner.ParallelAsync{TResult,T}" />
    public static async Task<List<TResult>> ParallelAsync<T, TResult>(
        this IEnumerable<T> items,
        Func<T, int, Task<TResult>> action,
        int? maxConcurrent = null)
    {
        return await Util.TaskRunner.ParallelAsync(items, action, maxConcurrent);
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static async Task ForEachAsync<T, TActionResult>(this IEnumerable<T> items, Func<T, int, Task<TActionResult>> action)
    {
        var itemsList = items.As<IList<T>>() ?? items.ToList();

        for (var i = 0; i < itemsList.Count; i++) await action(itemsList[i], i);
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

    /// <summary>
    /// Executes an asynchronous action for each item in the list.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    /// <param name="items">The list of items to perform the action on.</param>
    /// <param name="action">The asynchronous action to perform on each item. The action takes two parameters: the item and its index in the list.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <example>
    ///     <code>
    /// await list.ForEachAsync((item, itemIndex) => do some thing async)
    /// </code>
    /// </example>
    public static void CloneForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        items.ToList().ForEach((item, index) => action(item));
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

    /// <summary>
    /// Executes an asynchronous action on each item of the provided collection in parallel, with a maximum number of concurrent operations.
    /// </summary>
    /// <typeparam name="T">The type of the items in the collection.</typeparam>
    /// <param name="items">The collection of items to perform the action on.</param>
    /// <param name="action">The asynchronous action to perform on each item.</param>
    /// <param name="maxConcurrent">The maximum number of concurrent operations. Defaults to <see cref="Util.TaskRunner.DefaultNumberOfParallelIoTasksPerCpuRatio" />.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method uses the <see cref="Parallel" /> class to execute the action in parallel.
    /// The order in which the action is applied to each item is not guaranteed.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the items collection or the action is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxConcurrent is less than 1.</exception>
    public static Task ParallelAsync<T>(this IEnumerable<T> items, Func<T, Task> action, int? maxConcurrent = null)
    {
        return items.ParallelAsync((item, index) => action(item), maxConcurrent);
    }

    /// <summary>
    /// Executes a provided asynchronous action in parallel for each item in the given IEnumerable{T}
    /// collection,
    /// and returns a list of results of type TResult. The number of concurrent tasks is limited by the maxConcurrent parameter.
    /// </summary>
    /// <typeparam name="T">The type of the items in the collection.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the action.</typeparam>
    /// <param name="items">The IEnumerable[T] collection on which the action is to be executed.</param>
    /// <param name="action">The asynchronous action to be executed on each item in the collection. The action takes an item of type T and returns a Task of TResult.</param>
    /// <param name="maxConcurrent">The maximum number of concurrent tasks. The default value is DefaultParallelAsyncMaxConcurrent.</param>
    /// <returns>A Task that represents the asynchronous operation and contains a List of TResult as the result of the action executed on each item in the collection.</returns>
    public static Task<List<TResult>> ParallelAsync<T, TResult>(
        this IEnumerable<T> items,
        Func<T, Task<TResult>> action,
        int? maxConcurrent = null)
    {
        return items.ParallelAsync((item, index) => action(item), maxConcurrent);
    }

    /// <inheritdoc cref="ForEachAsync{T}(IEnumerable{T},Func{T,int,Task})" />
    public static Task ForEachAsync<T, TActionResult>(this IList<T> items, Func<T, Task<TActionResult>> action)
    {
        return items.ForEachAsync((item, index) => action(item));
    }

    /// <summary>
    /// Asynchronously transforms each element of a sequence to a new form.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TActionResult">The type of the value returned by the transform function.</typeparam>
    /// <param name="items">An IEnumerable{T} to invoke a transform function on.</param>
    /// <param name="actionAsync">An asynchronous transform function to apply to each element.</param>
    /// <returns>A Task[List[TActionResult]] that represents the asynchronous operation. The task result contains a List{TActionResult} with each element transformed to a new form.</returns>
    /// <example>
    ///     <code>
    /// var listB = await list.SelectAsync((item, itemIndex) => get B async)
    /// </code>
    /// </example>
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
    /// Transforms each element of an asynchronous sequence into a new form by incorporating the element's index.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the source sequence.</typeparam>
    /// <typeparam name="TActionResult">The type of the value returned by the selector function.</typeparam>
    /// <param name="itemsTask">A task that represents the asynchronous operation and returns an enumerable collection.</param>
    /// <param name="selector">A transform function to apply to each element; the second parameter of the function represents the index of the source element.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list that contains the result of invoking the transform function on each element of the source sequence in the original order.</returns>
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<IEnumerable<T>> itemsTask,
        Func<T, int, TActionResult> selector)
    {
        return itemsTask.Then(items => items.Select(selector).ToList());
    }

    /// <summary>
    /// Transforms each element of a sequence to a new form.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TActionResult">The type of the value returned by the selector function.</typeparam>
    /// <param name="itemsTask">A task that represents a sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A task that represents a sequence of the transformed elements.</returns>
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<IEnumerable<T>> itemsTask,
        Func<T, TActionResult> selector)
    {
        return itemsTask.ThenSelect((item, index) => selector(item));
    }

    /// <summary>
    /// Transforms each element of an asynchronous sequence into a new form by incorporating the element's index.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the source sequence.</typeparam>
    /// <typeparam name="TActionResult">The type of the value returned by the selector function.</typeparam>
    /// <param name="itemsTask">A task that represents the asynchronous operation and returns a list.</param>
    /// <param name="selector">A transform function to apply to each element; the second parameter of the function represents the index of the source element.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list that contains the result of invoking the transform function on each element of the source sequence in the original order.</returns>
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, int, TActionResult> selector)
    {
        return itemsTask.Then(items => items.Select(selector).ToList());
    }

    /// <summary>
    /// Asynchronously transforms the elements of a list into a new list based on a provided asynchronous selector function.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source list.</typeparam>
    /// <typeparam name="TActionResult">The type of the elements in the resulting list.</typeparam>
    /// <param name="itemsTask">A task that represents the asynchronous operation and returns a list of items.</param>
    /// <param name="selector">An asynchronous function to apply to each element of the source list.</param>
    /// <returns>A task that represents the asynchronous operation and returns a new list.</returns>
    public static Task<List<TActionResult>> ThenSelectAsync<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, int, Task<TActionResult>> selector)
    {
        return itemsTask.Then(items => items.SelectAsync(selector));
    }

    /// <summary>
    /// Transforms each element of an asynchronous sequence into a new form.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the source sequence.</typeparam>
    /// <typeparam name="TActionResult">The type of the value returned by the selector function.</typeparam>
    /// <param name="itemsTask">A task that represents the asynchronous operation and returns a list.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list that contains the result of invoking the transform function on each element of the source sequence in the original order.</returns>
    public static Task<List<TActionResult>> ThenSelect<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, TActionResult> selector)
    {
        return itemsTask.ThenSelect((item, index) => selector(item));
    }

    /// <summary>
    /// Asynchronously transforms the elements of a list into a new list based on a provided asynchronous selector function.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source list.</typeparam>
    /// <typeparam name="TActionResult">The type of the elements in the resulting list.</typeparam>
    /// <param name="itemsTask">A task that represents the asynchronous operation and returns a list of items.</param>
    /// <param name="selector">An asynchronous function to apply to each element of the source list.</param>
    /// <returns>A task that represents the asynchronous operation and returns a new list.</returns>
    public static Task<List<TActionResult>> ThenSelectAsync<T, TActionResult>(
        this Task<List<T>> itemsTask,
        Func<T, Task<TActionResult>> selector)
    {
        return itemsTask.ThenSelectAsync((item, index) => selector(item));
    }

    /// <summary>
    /// Transforms each element in the list using the provided mapping function and returns a new list of the transformed elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source list.</typeparam>
    /// <typeparam name="T1">The type of elements in the resulting list.</typeparam>
    /// <param name="items">The source list to be transformed.</param>
    /// <param name="mapFunc">A function that defines the transformation to be applied to each element in the source list.</param>
    /// <returns>A new list of elements of type T1, each of which is the result of applying the mapping function to a corresponding element in the source list.</returns>
    public static List<T1> Map<T, T1>(this IList<T> items, Func<T, T1> mapFunc)
    {
        return items.Select(mapFunc).ToList();
    }

    /// <summary>
    /// Flattens a sequence of sequences into a single sequence.
    /// </summary>
    /// <param name="items">The sequence of sequences to flatten.</param>
    /// <returns>A single sequence that contains the elements of each contained sequence.</returns>
    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
    {
        return items.SelectMany(p => p);
    }

    /// <summary>
    /// Converts an IEnumerable of items into a dictionary and a list of keys.
    /// </summary>
    /// <typeparam name="T">The type of items in the IEnumerable.</typeparam>
    /// <typeparam name="TKey">The type of the key in the dictionary.</typeparam>
    /// <param name="items">The IEnumerable of items to convert.</param>
    /// <param name="selectKey">A function to select the key for each item.</param>
    /// <returns>A tuple containing the dictionary and the list of keys.</returns>
    public static ValueTuple<Dictionary<TKey, T>, List<TKey>> ToDictionaryWithKeysList<T, TKey>(this IEnumerable<T> items, Func<T, TKey> selectKey) where TKey : notnull
    {
        var dict = items.ToDictionary(selectKey, p => p);
        var keys = dict.Keys.ToList();

        return (dict, keys);
    }

    /// <summary>
    /// Transforms each element of a sequence to a new form and returns the result as a list.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by the selector function.</typeparam>
    /// <param name="source">An IEnumerable&lt;TSource&gt; to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A List&lt;TResult&gt; whose elements are the result of invoking the transform function on each element of source.</returns>
    public static List<TResult> SelectList<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        return source.Select(selector).ToList();
    }

    /// <summary>
    /// Transforms the elements in the source collection into a HashSet using the provided selector function.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of the elements in the resulting HashSet.</typeparam>
    /// <param name="source">The source collection to transform.</param>
    /// <param name="selector">A function to transform elements from the source collection into elements in the resulting HashSet.</param>
    /// <returns>A HashSet containing the transformed elements from the source collection.</returns>
    public static HashSet<TResult> SelectHashset<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        return source.Select(selector).ToHashSet();
    }

    /// <summary>
    /// Concatenates the second sequence to the first sequence if the specified condition is true.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <param name="source">The first sequence to concatenate.</param>
    /// <param name="if">A boolean value that represents the condition.</param>
    /// <param name="second">The sequence to concatenate to the first sequence if the condition is true.</param>
    /// <returns>A sequence that contains the concatenated elements of the two input sequences if the condition is true, otherwise the first sequence.</returns>
    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, bool @if, IEnumerable<TSource> second)
    {
        return @if ? source.Concat(second) : source;
    }

    /// <summary>
    /// Concatenates the second sequence to the first sequence if the specified condition is true.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <param name="source">The first sequence to concatenate.</param>
    /// <param name="if">A boolean value that represents the condition.</param>
    /// <param name="second">The sequence to concatenate to the first sequence if the condition is true.</param>
    /// <returns>A sequence that contains the concatenated elements of the two input sequences if the condition is true, otherwise the first sequence.</returns>
    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, bool @if, params TSource[] second)
    {
        return ConcatIf(source, @if, second.ToList());
    }

    /// <summary>
    /// Concatenates the second sequence to the first sequence if the specified condition is true.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <param name="source">The first sequence to concatenate.</param>
    /// <param name="if">A function that accepts the first sequence and returns a boolean value representing the condition.</param>
    /// <param name="second">A function that accepts the first sequence and returns the sequence to concatenate to the first sequence if the condition is true.</param>
    /// <returns>A sequence that contains the concatenated elements of the two input sequences if the condition is true, otherwise the first sequence.</returns>
    public static IEnumerable<TSource> ConcatIf<TSource>(
        this IEnumerable<TSource> source,
        Func<IEnumerable<TSource>, bool> @if,
        Func<IEnumerable<TSource>, IEnumerable<TSource>> second)
    {
        var sourceList = source.As<IList<TSource>>() ?? source.ToList();

        return @if(sourceList) ? sourceList.Concat(second(sourceList)) : sourceList;
    }

    /// <summary>
    /// Concatenates the elements of the specified array to the end of the source sequence if the specified condition is met.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of the source sequence.</typeparam>
    /// <param name="source">The sequence to concatenate elements to.</param>
    /// <param name="if">A function to test each element for a condition.</param>
    /// <param name="second">The array whose elements should be concatenated to the end of the source sequence if the condition is met.</param>
    /// <returns>An <see cref="IEnumerable{TSource}" /> that contains the concatenated elements of the two sequences if the condition is met.</returns>
    public static IEnumerable<TSource> ConcatIf<TSource>(this IEnumerable<TSource> source, Func<IEnumerable<TSource>, bool> @if, params TSource[] second)
    {
        return ConcatIf(source, @if, p => second);
    }

    /// <summary>
    /// Excludes the specified items from the original list.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    /// <param name="items">The original list of items.</param>
    /// <param name="excludeItems">The items to be excluded.</param>
    /// <returns>A new list that contains the items from the original list excluding the specified items.</returns>
    public static List<T> Exclude<T>(this IList<T> items, IList<T> excludeItems)
    {
        var excludeItemsHashSet = excludeItems.ToHashSet();
        return items
            .Where(p => !excludeItemsHashSet.Contains(p))
            .ToList();
    }

    /// <summary>
    /// Converts the elements of an IEnumerable to a string and concatenates them with a specified separator.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the IEnumerable.</typeparam>
    /// <param name="items">The IEnumerable containing the elements to concatenate.</param>
    /// <param name="separator">The string to use as a separator. The separator is included in the returned string only if the IEnumerable has more than one element.</param>
    /// <returns>A string that consists of the elements in the IEnumerable delimited by the separator string. If the IEnumerable is null or contains no elements, the method returns String.Empty.</returns>
    public static string JoinToString<T>(this IEnumerable<T>? items, string separator = "") where T : notnull
    {
        return items != null ? string.Join(separator, items.Select(p => p?.ToString() ?? string.Empty)) : string.Empty;
    }

    /// <summary>
    /// Concatenates the members of a collection, using the specified separator between each member.
    /// </summary>
    /// <typeparam name="T">The type of the members of the collection.</typeparam>
    /// <param name="items">The collection whose members to concatenate.</param>
    /// <param name="separator">The string to use as a separator. separator is included in the returned string only if items has more than one element.</param>
    /// <returns>A string that consists of the members of items delimited by the separator string. If items has no members, the method returns String.Empty.</returns>
    public static string JoinToString<T>(this IEnumerable<T> items, char separator) where T : notnull
    {
        return JoinToString(items, separator.ToString());
    }

    // Add this to fix ambiguous evocation with other library by help compiler to select exact type extension
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this List<T>? items)
    {
        return items == null || !items.Any();
    }

    // Add this to fix ambiguous evocation with other library by help compiler to select exact type extension
    public static bool IsNullOrEmpty<TKey, TValue>([NotNullWhen(false)] this Dictionary<TKey, TValue>? items) where TKey : notnull
    {
        return items == null || !items.Any();
    }

    /// <summary>
    /// Enumerates the elements of a collection and provides their indices.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source collection.</typeparam>
    /// <param name="source">The source collection to enumerate.</param>
    /// <returns>An IEnumerable of tuples, where each tuple contains an item from the source collection and its index.</returns>
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }

    /// <summary>
    /// Asynchronously projects each element of a sequence to an IAsyncEnumerable&lt;TResult&gt; and flattens the resulting sequences into one sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by selector.</typeparam>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>
    /// An IAsyncEnumerable&lt;TResult&gt; whose elements are the result of invoking the one-to-many transform function on each element of the input sequence.
    /// </returns>
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
            .PipeIf(skipCount >= 0, e => e.Skip(skipCount!.Value))
            .PipeIf(maxResultCount >= 0, e => e.Take(maxResultCount!.Value));
    }

    public static IEnumerable<List<T>> PagedGroups<T>(this IEnumerable<T> query, int pageSize)
    {
        var page = new List<T>(pageSize);

        foreach (var item in query)
        {
            page.Add(item);

            if (page.Count == pageSize)
            {
                yield return page;
                page = new List<T>(pageSize); // Reset the page for the next group
            }
        }

        // Yield any remaining items in the last page
        if (page.Count > 0) yield return page;
    }

    /// <summary>
    /// Determines whether all elements of a sequence satisfy a condition.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An <see cref="IEnumerable{T}" /> that contains the elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <returns>true if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, false.</returns>
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

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An <see cref="IEnumerable{T}" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <returns>default(<typeparamref name="TSource" />) if source is empty or if no element passes the test specified by predicate; otherwise, the first element in source that passes the test specified by predicate.</returns>
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

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">An <see cref="IEnumerable{T}" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <returns>The first element in the sequence that passes the test in the specified predicate function.</returns>
    /// <exception cref="Exception">No element satisfies the condition in the predicate function.</exception>
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

    /// <summary>
    /// Splits the source list into two lists based on the provided predicate.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source list.</typeparam>
    /// <param name="source">The source list to be split.</param>
    /// <param name="predicate">The function to test each element for a condition.</param>
    /// <returns>A tuple containing two lists: the first list contains elements that satisfy the condition, and the second list contains the remaining elements.</returns>
    public static ValueTuple<List<TSource>, List<TSource>> FilterSplitResult<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        var matchedFilterResult = new List<TSource>();
        var remainingFilterResult = new List<TSource>();

        source.ForEach(
            item =>
            {
                if (predicate(item)) matchedFilterResult.Add(item);
                else remainingFilterResult.Add(item);
            });

        return (matchedFilterResult, remainingFilterResult);
    }

    /// <summary>
    /// Determines whether the source object is a non-empty collection of certain types.
    /// </summary>
    /// <param name="source">The source object to check.</param>
    /// <returns>
    /// true if the source object is a non-empty collection of type object, int, float, double, string, or DateTime; otherwise, false.
    /// </returns>
    public static bool IsHasItemsCollections(this object source)
    {
        return (source is IEnumerable<object> objectList && objectList.Any()) ||
               (source is IEnumerable<int> intList && intList.Any()) ||
               (source is IEnumerable<float> floatList && floatList.Any()) ||
               (source is IEnumerable<double> doubleList && doubleList.Any()) ||
               (source is IEnumerable<string> stringList && stringList.Any()) ||
               (source is IEnumerable<DateTime> dateList && dateList.Any());
    }

    /// <summary>
    /// Determines whether all elements of a list are part of another collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list1">The list to check for its elements' existence in the second collection.</param>
    /// <param name="list2">The collection in which to locate the elements of the first list.</param>
    /// <returns>true if every element of the first list is found in the second collection; otherwise, false.</returns>
    public static bool PartOf<T>(this IList<T> list1, IEnumerable<T> list2)
    {
        return list2.ContainsAll(list1);
    }

    /// <summary>
    /// Generates a sequence of integers from 0 to the specified number (less than, not include).
    /// </summary>
    /// <typeparam name="TNumber">The type of the number. Must implement INumber&lt;TNumber&gt;.</typeparam>
    /// <param name="number">The number to generate the sequence to.</param>
    /// <returns>An IEnumerable sequence of integers from 0 to the specified number (less than, not include).</returns>
    public static IEnumerable<int> ToRange<TNumber>(this TNumber number) where TNumber : INumber<TNumber>
    {
        for (var i = 0; number.CompareTo(i) > 0; i++) yield return i;
    }

    public static bool ContainsIgnoreCase(this IEnumerable<string> list, string value)
    {
        return list.Any(p => p.EqualsIgnoreCase(value));
    }

    /// <inheritdoc cref="WhereSplitResult{T}(IEnumerable{T},Expression{Func{T,bool}})" />
    public static ValueTuple<List<T>, List<T>> WhereSplitResult<T>(this IEnumerable<T> items, Expression<Func<T, bool>> predicate)
    {
        return WhereSplitResult(items.ToList(), predicate.Compile());
    }

    /// <summary>
    /// Splits the input list into two separate lists based on a given predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the input list.</typeparam>
    /// <param name="list">The source list to be split.</param>
    /// <param name="predicate">
    /// A lambda expression used to determine which items should go into the first list (those that match the predicate).
    /// </param>
    /// <returns>
    /// A <see cref="ValueTuple" /> containing two lists:
    /// <list type="bullet">
    ///     <item>
    ///         <description>The first list contains items that satisfy the predicate.</description>
    ///     </item>
    ///     <item>
    ///         <description>The second list contains items that do not satisfy the predicate.</description>
    ///     </item>
    /// </list>
    /// </returns>
    public static ValueTuple<List<T>, List<T>> WhereSplitResult<T>(this ICollection<T> list, Func<T, bool> predicateFn)
    {
        var matchItems = new List<T>();
        var notMatchItems = new List<T>();

        foreach (var item in list)
        {
            if (predicateFn(item)) matchItems.Add(item);
            else notMatchItems.Add(item);
        }

        return (matchItems, notMatchItems);
    }


    public static List<object> ToObjectList(this ICollection collection)
    {
        var result = new List<object>(collection.Count);

        foreach (var item in collection) result.Add(item);

        return result;
    }

    public static List<DictionaryEntry> ToEntryItemList(this IDictionary dictionary)
    {
        var result = new List<DictionaryEntry>(dictionary.Count);

        foreach (DictionaryEntry item in dictionary) result.Add(item);

        return result;
    }
}
