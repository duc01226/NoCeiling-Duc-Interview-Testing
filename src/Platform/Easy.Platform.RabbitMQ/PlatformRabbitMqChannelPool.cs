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
    protected readonly ConcurrentDictionary<int, IModel> CreatedChannelDict = new();

    protected readonly SemaphoreSlim InitInternalObjectPoolLock = new(1, 1);
    protected PlatformRabbitMqChannelPoolPolicy ChannelPoolPolicy;
    protected DefaultObjectPool<IModel> InternalObjectPool;
    private bool disposed;

    public PlatformRabbitMqChannelPool(PlatformRabbitMqChannelPoolPolicy channelPoolPolicy)
    {
        ChannelPoolPolicy = channelPoolPolicy;
    }

    public int PoolSize { get; set; } = Environment.ProcessorCount * 2;

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

    private void InitInternalObjectPool()
    {
        if (InternalObjectPool == null)
            InitInternalObjectPoolLock.ExecuteLockAction(() => InternalObjectPool ??= new DefaultObjectPool<IModel>(ChannelPoolPolicy, PoolSize));
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
