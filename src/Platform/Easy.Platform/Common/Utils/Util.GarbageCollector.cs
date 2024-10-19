using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class GarbageCollector
    {
        public const int DefaultCollectGarbageMemoryThrottleSeconds = 3;
        private static readonly ConcurrentDictionary<double, TaskRunner.Throttler> CollectGarbageMemoryThrottlerDict = new();

        public static void Collect(double throttleSeconds = DefaultCollectGarbageMemoryThrottleSeconds, bool collectAggressively = false)
        {
            if (throttleSeconds <= 0)
            {
                DoCollect(collectAggressively);
            }
            else
            {
                var throttleTime = SetupCollectGarbageMemoryThrottlerDict(throttleSeconds);

                // Delay a bit to ensure caller function collect memory has been returned
                _ = CollectGarbageMemoryThrottlerDict[throttleTime]
                    .ThrottleExecute(
                        () => Task.Delay(100).ThenAction(() => DoCollect(collectAggressively)));
            }
        }

        private static void DoCollect(bool collectAggressively)
        {
            if (collectAggressively)
            {
                GC.Collect(0, GCCollectionMode.Forced, true, true); // Generation 0, blocking, compacting LOH
                GC.WaitForPendingFinalizers(); // Wait for all finalizers to finish
                GC.Collect(0, GCCollectionMode.Forced, true, true); // Run GC again to ensure complete cleanup

                GC.Collect(1, GCCollectionMode.Forced, true, true); // Generation 1, blocking, compacting LOH
                GC.WaitForPendingFinalizers(); // Wait for all finalizers to finish
                GC.Collect(1, GCCollectionMode.Forced, true, true); // Run GC again to ensure complete cleanup

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true); // MaxGeneration, blocking, compacting LOH
                GC.WaitForPendingFinalizers(); // Wait for all finalizers to finish
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true); // Run GC again to ensure complete cleanup
            }
            else
            {
                GC.Collect(0, GCCollectionMode.Optimized, true, true); // Generation 0, blocking, compacting LOH
                GC.WaitForPendingFinalizers(); // Wait for all finalizers to finish
                GC.Collect(0, GCCollectionMode.Optimized, true, true); // Run GC again to ensure complete cleanup
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
