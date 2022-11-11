namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class Pager
    {
        /// <summary>
        /// Support execute async action paged.
        /// </summary>
        /// <param name="executeFn">Execute function async. Input is: skipCount, pageSize.</param>
        /// <param name="maxItemCounts">Max items count</param>
        /// <param name="pageSize">Page size to execute.</param>
        /// <returns>Task.</returns>
        public static async Task ExecutePagingAsync(
            Func<int, int, Task> executeFn,
            long maxItemCounts,
            int pageSize)
        {
            var currentSkipItems = 0;

            do
            {
                await executeFn(currentSkipItems, pageSize);
                currentSkipItems += pageSize;
            } while (currentSkipItems < maxItemCounts);
        }

        /// <summary>
        /// Support execute async action paged until no items left or reach to maxExecutionCount.
        /// </summary>
        /// <typeparam name="TItem">The item type to be processed</typeparam>
        /// <param name="getItemsPackageFn">Get a partial/page/package items</param>
        /// <param name="executeFn">Execute function async. Input is: items.</param>
        /// <param name="maxExecutionCount">Max execution count. Default is <see cref="ulong.MaxValue"/></param>
        /// <returns></returns>
        public static async Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<IEnumerable<TItem>>> getItemsPackageFn,
            Func<IEnumerable<TItem>, Task> executeFn,
            ulong maxExecutionCount = ulong.MaxValue)
        {
            ulong totalExecutionCount = 0;
            var currentPackageItems = (await getItemsPackageFn()).ToList();

            while (totalExecutionCount < maxExecutionCount && currentPackageItems.Any())
            {
                await executeFn(currentPackageItems);

                totalExecutionCount += 1;
                if (totalExecutionCount < maxExecutionCount)
                    currentPackageItems = (await getItemsPackageFn()).ToList();
            }
        }

        /// <summary>
        /// <see cref="ExecuteScrollingPagingAsync{TItem}(Func{Task{IEnumerable{TItem}}},Func{IEnumerable{TItem},Task},ulong)"/>
        /// </summary>
        public static Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<List<TItem>>> getItemsPackageFn,
            Func<List<TItem>, Task> executeFn,
            ulong maxExecutionCount = ulong.MaxValue)
        {
            return ExecuteScrollingPagingAsync(
                GetItemsPackageFacadeFn,
                items => executeFn(items.ToList()),
                maxExecutionCount);

            async Task<IEnumerable<TItem>> GetItemsPackageFacadeFn()
            {
                return await getItemsPackageFn();
            }
        }
    }
}
