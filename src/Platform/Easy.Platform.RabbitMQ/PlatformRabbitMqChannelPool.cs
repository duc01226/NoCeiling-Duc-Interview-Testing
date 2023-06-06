using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client;

namespace Easy.Platform.RabbitMQ;

/// <summary>
/// Use ObjectBool to manage chanel because HostService is singleton, and we don't want re-init chanel is heavy and wasting time.
/// We want to use pool when object is expensive to allocate/initialize
/// References: https://docs.microsoft.com/en-us/aspnet/core/performance/objectpool?view=aspnetcore-5.0
/// </summary>
public class PlatformRabbitMqChannelPool : IDisposable
{
    protected readonly ConcurrentDictionary<int, IModel> CachedChannelPerThreadDict = new();
    protected readonly PlatformRabbitMqChannelPoolPolicy ChannelPoolPolicy;
    protected readonly ConcurrentDictionary<int, IModel> CreatedChannelDict = new();

    protected readonly SemaphoreSlim InitInternalObjectPoolLock = new(1, 1);
    protected DefaultObjectPool<IModel> InternalObjectPool;

    public PlatformRabbitMqChannelPool(PlatformRabbitMqChannelPoolPolicy channelPoolPolicy)
    {
        ChannelPoolPolicy = channelPoolPolicy;
    }

    public int MaximumRetained { get; set; } = Environment.ProcessorCount * 2;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IModel Get()
    {
        InitInternalObjectPool();

        var channel = InternalObjectPool!.Get();

        CreatedChannelDict.TryAdd(channel.ChannelNumber, channel);

        return channel;
    }

    public IModel GetCachedChannelPerThread()
    {
        return CachedChannelPerThreadDict.GetOrAdd(Environment.CurrentManagedThreadId, threadId => Get());
    }

    private void InitInternalObjectPool()
    {
        if (InternalObjectPool == null)
            InitInternalObjectPoolLock.ExecuteLockAction(() => InternalObjectPool ??= new DefaultObjectPool<IModel>(ChannelPoolPolicy, MaximumRetained));
    }

    public void Return(IModel obj)
    {
        InternalObjectPool.Return(obj);
    }

    public void TryInitFirstChannel()
    {
        var tryGetChannelTestSuccess = Get();

        Return(tryGetChannelTestSuccess);
    }

    public void GetChannelDoActionAndReturn(Action<IModel> action)
    {
        var channel = Get();

        try
        {
            action(channel);
        }
        finally
        {
            Return(channel);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            CachedChannelPerThreadDict.ForEach(p => p.Value.Dispose());
            CreatedChannelDict.ForEach(p => p.Value.Dispose());
            ChannelPoolPolicy?.Dispose();
        }
    }
}
