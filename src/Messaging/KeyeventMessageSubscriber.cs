using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal class KeyeventMessageSubscriber :
    IMessageSubscriber
{
    public KeyeventMessageSubscriber(
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public EventHandler<OnMessageEventArgs>? OnMessage { get; set; }
    public EventHandler? OnSubscribe { get; set; }

    public async Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:del",
                    RedisChannel.PatternMode.Pattern),
                ProcessMessage)
            .ConfigureAwait(false);

        await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:hset",
                    RedisChannel.PatternMode.Pattern),
                ProcessMessage)
            .ConfigureAwait(false);

        OnSubscribe?.Invoke(
            this,
            EventArgs.Empty);
    }

    public async Task UnsubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        await connectionMultiplexer
            .GetSubscriber()
            .UnsubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:del",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false);

        await connectionMultiplexer
            .GetSubscriber()
            .UnsubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:hset",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false);
    }

    internal void ProcessMessage(
        RedisChannel channel,
        RedisValue message)
    {
        if (message.StartsWith(
                L1L2RedisCacheOptions.KeyPrefix))
        {
            var key = message
                .ToString()[L1L2RedisCacheOptions.KeyPrefix.Length..];
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");

            OnMessage?.Invoke(
                this,
                new OnMessageEventArgs(key));
        }
    }
}
