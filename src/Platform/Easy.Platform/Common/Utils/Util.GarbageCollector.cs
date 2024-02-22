namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class GarbageCollector
    {
        private static readonly TaskRunner.Throttler CollectGarbageMemoryThrottler = new();

        public static readonly int DefaultCollectGarbageMemoryThrottleSeconds = 1;

        public static void Collect(int? throttleSeconds = null, bool aggressiveImmediately = false)
        {
            if (aggressiveImmediately)
            {
                GC.Collect();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                return;
            }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CollectGarbageMemoryThrottler.ThrottleExecuteAsync(
                () => Task.Run(GC.Collect),
                TimeSpan.FromSeconds(throttleSeconds ?? DefaultCollectGarbageMemoryThrottleSeconds));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public static void Collect(
            int generation,
            GCCollectionMode mode,
            bool blocking,
            bool compacting,
            int? throttleSeconds = null,
            bool immediately = false)
        {
            if (immediately)
            {
                GC.Collect(generation, mode, blocking, compacting);
                return;
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CollectGarbageMemoryThrottler.ThrottleExecuteAsync(
                () => Task.Run(() => GC.Collect(generation, mode, blocking, compacting)),
                TimeSpan.FromSeconds(throttleSeconds ?? DefaultCollectGarbageMemoryThrottleSeconds));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
