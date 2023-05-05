using Easy.Platform.Common.Extensions;

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

                GC.Collect();
            } while (currentSkipItems < maxItemCounts);
        }

        /// <summary>
        /// Support execute async action paged with return value
        /// </summary>
        /// <param name="executeFn">Execute function async. Input is: skipCount, pageSize.</param>
        /// <param name="maxItemCounts">Max items count</param>
        /// <param name="pageSize">Page size to execute.</param>
        /// <returns>Task.</returns>
        public static async Task<List<TPagedResult>> ExecutePagingAsync<TPagedResult>(
            Func<int, int, Task<TPagedResult>> executeFn,
            long maxItemCounts,
            int pageSize)
        {
            var currentSkipItems = 0;
            var result = new List<TPagedResult>();

            do
            {
                var pagedResult = await executeFn(currentSkipItems, pageSize);

                result.Add(pagedResult);

                currentSkipItems += pageSize;

                GC.Collect();
            } while (currentSkipItems < maxItemCounts);

            return result;
        }

        /// <summary>
        /// Execute until the executeFn return no items
        /// </summary>
        public static async Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<IEnumerable<TItem>>> executeFn,
            int maxExecutionCount = int.MaxValue)
        {
            await ExecuteScrollingPagingAsync(executeFn: () => executeFn().Then(_ => _.ToList()), maxExecutionCount);
        }

        /// <summary>
        /// Execute until the executeFn return no items
        /// </summary>
        public static async Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<List<TItem>>> executeFn,
            int maxExecutionCount = int.MaxValue)
        {
            var totalExecutionCount = 0;
            var executionItemsResult = await executeFn();

            while (totalExecutionCount < maxExecutionCount && executionItemsResult.Any())
            {
                executionItemsResult = await executeFn();
                totalExecutionCount += 1;

                GC.Collect();
            }
        }
    }
}
