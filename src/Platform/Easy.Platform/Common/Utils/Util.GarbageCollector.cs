using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class GarbageCollector
    {
        public const int DefaultCollectGarbageMemoryThrottleSeconds = 1;
        private static readonly ConcurrentDictionary<double, TaskRunner.Throttler> CollectGarbageMemoryThrottlerDict = new();
        private static readonly object LockCollectObj = new();

        public static void Collect(double throttleSeconds = DefaultCollectGarbageMemoryThrottleSeconds, bool collectAggressively = false)
        {
            if (throttleSeconds <= 0)
            {
                DoCollect(collectAggressively);
            }
            else
            {
                var throttleTime = SetupCollectGarbageMemoryThrottlerDict(throttleSeconds);

                CollectGarbageMemoryThrottlerDict[throttleTime].ThrottleExecute(() => DoCollect(collectAggressively));
            }
        }

        private static void DoCollect(bool collectAggressively)
        {
            lock (LockCollectObj)
            {
                // Force garbage collection
                /*
                 * This method forces garbage collection to occur.
                 * It does not guarantee immediate reclamation of all unused objects,
                 * but it schedules a garbage collection to happen as soon as possible.
                 * It can be called with no arguments, which triggers a full blocking garbage collection of all generations,
                 * or with a specific generation parameter to target a specific generation.
                 */
                GC.Collect();

                // Wait for finalizers to complete
                /*
                 *Finalization is the process of cleaning up unmanaged resources associated with an object before it is reclaimed by the garbage collector.
                 * Objects with finalizers are placed on a finalization queue when they become eligible for garbage collection.
                 * This method blocks the calling thread until all objects in the finalization queue have been finalized.
                 * It's often used in conjunction with GC.Collect() to ensure that finalization has completed before continuing.
                 */
                GC.WaitForPendingFinalizers();

                // Wait for full garbage collection to complete
                /*
                 * This method blocks the calling thread until a full garbage collection (including all generations) has completed.
                 * It waits until all background garbage collection threads have finished their work.
                 * This is useful when you want to ensure that all garbage collection activity has ceased before proceeding.
                 */
                GC.WaitForFullGCComplete();

                if (collectAggressively)
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.WaitForFullGCComplete();
                }
            }
        }

        private static double SetupCollectGarbageMemoryThrottlerDict(double throttleSeconds)
        {
            if (!CollectGarbageMemoryThrottlerDict.ContainsKey(throttleSeconds))
                CollectGarbageMemoryThrottlerDict.TryAdd(throttleSeconds, new TaskRunner.Throttler(throttleSeconds.Seconds()));

            return throttleSeconds;
        }
    }
}
