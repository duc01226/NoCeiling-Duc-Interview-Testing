using System.Collections.Concurrent;
using Easy.Platform.RabbitMQ.Extensions;
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
    protected readonly ConcurrentDictionary<int, IChannel> CreatedChannelDict = new();

    protected readonly SemaphoreSlim InitInternalObjectPoolLock = new(1, 1);
    protected PlatformRabbitMqChannelPoolPolicy ChannelPoolPolicy;
    protected DefaultObjectPool<IChannel> InternalObjectPool;
    private bool disposed;

    public PlatformRabbitMqChannelPool(PlatformRabbitMqChannelPoolPolicy channelPoolPolicy)
    {
        ChannelPoolPolicy = channelPoolPolicy;
    }

    public int PoolSize => ChannelPoolPolicy.PoolSize;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IChannel Get()
    {
        InitInternalObjectPool();

        var channel = InternalObjectPool!.Get();

        // If channel IsClosedPermanently mean that this is an existing created channel in the pool which is used and being closed for some reason
        while (channel.IsClosedPermanently())
        {
            CreatedChannelDict.TryRemove(channel.ChannelNumber, out _);
            channel.Dispose();

            channel = InternalObjectPool!.Get();
        }

        CreatedChannelDict.TryAdd(channel.ChannelNumber, channel);

        return channel;
    }

    private void InitInternalObjectPool()
    {
        if (InternalObjectPool == null)
            InitInternalObjectPoolLock.ExecuteLockAction(() => InternalObjectPool ??= new DefaultObjectPool<IChannel>(ChannelPoolPolicy, PoolSize));
    }

    public void Return(IChannel obj)
    {
        InternalObjectPool.Return(obj);
    }

    public void TryInitFirstChannel()
    {
        var tryGetChannelTestSuccess = Get();

        Return(tryGetChannelTestSuccess);
    }

    public void GetChannelDoActionAndReturn(Action<IChannel> action)
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

    public async Task GetChannelDoActionAndReturn(Func<IChannel, Task> action)
    {
        var channel = Get();

        try
        {
            await action(channel);
        }
        finally
        {
            Return(channel);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Release managed resources
                CreatedChannelDict.ForEach(p => p.Value.Dispose());
                CreatedChannelDict.Clear();

                ChannelPoolPolicy?.Dispose();
                ChannelPoolPolicy = null;
            }

            // Release unmanaged resources

            disposed = true;
        }
    }

    ~PlatformRabbitMqChannelPool()
    {
        Dispose(false);
    }
}
