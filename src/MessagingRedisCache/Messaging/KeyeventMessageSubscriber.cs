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

    public async Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        (await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:del",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false))
            .OnMessage(ProcessMessageAsync);

        (await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:hset",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false))
            .OnMessage(ProcessMessageAsync);

        await MessagingRedisCacheOptions
            .Events
            .OnSubscribe
            .Invoke()
            .ConfigureAwait(false);
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

    internal async Task ProcessMessageAsync(
        ChannelMessage channelMessage)
    {
        if (string.IsNullOrEmpty(
                MessagingRedisCacheOptions.InstanceName) ||
            channelMessage.Message.StartsWith(
                MessagingRedisCacheOptions.InstanceName))
        {
            var key = channelMessage.Message
                .ToString()[ (MessagingRedisCacheOptions.InstanceName?.Length ?? 0).. ];
            MemoryCache.Remove(
                key);

            await MessagingRedisCacheOptions
                .Events
                .OnMessageRecieved
                .Invoke(channelMessage)
                .ConfigureAwait(false);
        }
    }
}
