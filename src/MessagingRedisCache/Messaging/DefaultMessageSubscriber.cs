using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace MessagingRedisCache;

internal class DefaultMessageSubscriber(
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
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
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
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
            .ConfigureAwait(false);
    }

    internal async Task ProcessMessageAsync(
        ChannelMessage channelMessage)
    {
        var cacheMessage = JsonSerializer
            .Deserialize(
                channelMessage.Message.ToString(),
                SourceGenerationContext.Default.CacheMessage);
        if (cacheMessage != null &&
            cacheMessage.PublisherId != MessagingRedisCacheOptions.Id)
        {
            MemoryCache.Remove(
                cacheMessage.Key);

            await MessagingRedisCacheOptions
                .Events
                .OnMessageRecieved
                .Invoke(channelMessage)
                .ConfigureAwait(false);
        }
    }
}
