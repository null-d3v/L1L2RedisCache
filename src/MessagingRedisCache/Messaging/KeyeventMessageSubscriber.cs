using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MessagingRedisCache;

internal class KeyeventMessageSubscriber(
    IMemoryCache memoryCache,
    IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor) :
    IMessageSubscriber
{
    public IMemoryCache MemoryCache { get; set; } =
        memoryCache;
    public MessagingRedisCacheOptions MessagingRedisCacheOptions { get; set; } =
        messagingRedisCacheOptionsAccessor.Value;
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
        if (string.IsNullOrEmpty(
                MessagingRedisCacheOptions.InstanceName) ||
            message.StartsWith(
                MessagingRedisCacheOptions.InstanceName))
        {
            var key = message
                .ToString()[(MessagingRedisCacheOptions.InstanceName?.Length ?? 0)..];
            MemoryCache.Remove(
                key);

            OnMessage?.Invoke(
                this,
                new OnMessageEventArgs(key));
        }
    }
}
