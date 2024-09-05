using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    /// <summary>
    /// Provides utility methods for executing asynchronous paged actions and scrolling paging.
    /// </summary>
    public static class Pager
    {
        /// <summary>
        /// Executes an asynchronous action in a paged manner.
        /// </summary>
        /// <param name="executeFn">Async function to execute. Takes parameters: skipCount, pageSize.</param>
        /// <param name="maxItemCount">Maximum number of items to process.</param>
        /// <param name="pageSize">Size of each page.</param>
        /// <param name="pageDelayTime">Optional delay time between pages.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task ExecutePagingAsync(
            Func<int, int, Task> executeFn,
            long maxItemCount,
            int pageSize,
            TimeSpan? pageDelayTime = null,
            CancellationToken cancellationToken = default)
        {
            var currentSkipItemsCount = 0;

            do
            {
                if (cancellationToken.IsCancellationRequested) return;

                await executeFn(currentSkipItemsCount, pageSize);
                currentSkipItemsCount += pageSize;

                GarbageCollector.Collect();

                if (currentSkipItemsCount < maxItemCount && pageDelayTime.HasValue)
                    await Task.Delay(pageDelayTime.Value, cancellationToken);
            } while (currentSkipItemsCount < maxItemCount);
        }

        /// <summary>
        /// Executes an asynchronous action in a paged manner with return values.
        /// </summary>
        /// <typeparam name="TPagedResult">Type of the paged result.</typeparam>
        /// <param name="executeFn">Async function to execute. Takes parameters: skipCount, pageSize.</param>
        /// <param name="maxItemCount">Maximum number of items to process.</param>
        /// <param name="pageSize">Size of each page.</param>
        /// <param name="pageDelayTime">Optional delay time between pages.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task representing the asynchronous operation. The result is a list of paged results.</returns>
        public static async Task<List<TPagedResult>> ExecutePagingAsync<TPagedResult>(
            Func<int, int, Task<TPagedResult>> executeFn,
            long maxItemCount,
            int pageSize,
            TimeSpan? pageDelayTime = null,
            CancellationToken cancellationToken = default)
        {
            var currentSkipItemsCount = 0;
            var result = new List<TPagedResult>();

            do
            {
                if (cancellationToken.IsCancellationRequested) return result;

                var pagedResult = await executeFn(currentSkipItemsCount, pageSize);

                result.Add(pagedResult);
                currentSkipItemsCount += pageSize;

                GarbageCollector.Collect();

                if (currentSkipItemsCount < maxItemCount && pageDelayTime.HasValue)
                    await Task.Delay(pageDelayTime.Value, cancellationToken);
            } while (currentSkipItemsCount < maxItemCount);

            return result;
        }

        /// <summary>
        /// Executes an asynchronous action repeatedly until it returns no items.
        /// </summary>
        /// <typeparam name="TItem">Type of items returned by the execute function.</typeparam>
        /// <param name="executeFn">Async function to execute.</param>
        /// <param name="maxExecutionCount">Maximum number of times to execute the function.</param>
        /// <param name="pageDelayTime">Optional delay time between executions.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<IEnumerable<TItem>>> executeFn,
            int maxExecutionCount,
            TimeSpan? pageDelayTime = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteScrollingPagingAsync(executeFn: () => executeFn().Then(i => i.ToList()), maxExecutionCount, pageDelayTime, cancellationToken);
        }

        /// <summary>
        /// Executes an asynchronous action repeatedly until it returns no items.
        /// </summary>
        /// <typeparam name="TItem">Type of items returned by the execute function.</typeparam>
        /// <param name="executeFn">Async function to execute.</param>
        /// <param name="maxExecutionCount">Maximum number of times to execute the function.</param>
        /// <param name="pageDelayTime">Optional delay time between executions.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task ExecuteScrollingPagingAsync<TItem>(
            Func<Task<List<TItem>>> executeFn,
            int maxExecutionCount,
            TimeSpan? pageDelayTime = null,
            CancellationToken cancellationToken = default)
        {
            var executionItemsResult = await executeFn();
            var totalExecutionCount = 1;

            while (totalExecutionCount <= maxExecutionCount && executionItemsResult.Any() && !cancellationToken.IsCancellationRequested)
            {
                if (pageDelayTime.HasValue) await Task.Delay(pageDelayTime.Value, cancellationToken);

                executionItemsResult = await executeFn();
                totalExecutionCount += 1;

                GarbageCollector.Collect();
            }
        }
    }
}
