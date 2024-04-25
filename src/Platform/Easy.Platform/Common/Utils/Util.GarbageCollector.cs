using System.Collections.Concurrent;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class GarbageCollector
    {
        public const int DefaultCollectGarbageMemoryThrottleSeconds = 5;
        private static readonly ConcurrentDictionary<int, TaskRunner.Throttler> CollectGarbageMemoryThrottlerDict = new();

        public static void Collect(int throttleSeconds = DefaultCollectGarbageMemoryThrottleSeconds, bool immediately = false)
        {
            if (immediately)
            {
                GC.Collect();
            }
            else
            {
                var throttleTime = SetupCollectGarbageMemoryThrottlerDict(throttleSeconds);

                CollectGarbageMemoryThrottlerDict[throttleTime].ThrottleExecute(GC.Collect);
            }
        }

        private static int SetupCollectGarbageMemoryThrottlerDict(int throttleSeconds)
        {
            if (!CollectGarbageMemoryThrottlerDict.ContainsKey(throttleSeconds))
                CollectGarbageMemoryThrottlerDict.TryAdd(throttleSeconds, new TaskRunner.Throttler(throttleSeconds.Seconds()));

            return throttleSeconds;
        }

        public static void CollectAggressive(int throttleSeconds = DefaultCollectGarbageMemoryThrottleSeconds, bool immediately = false)
        {
            if (immediately)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            }
            else
            {
                var throttleTime = SetupCollectGarbageMemoryThrottlerDict(throttleSeconds);

                CollectGarbageMemoryThrottlerDict[throttleTime].ThrottleExecute(() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true));
            }
        }
    }
}
