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
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal),
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
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
            .ConfigureAwait(false);
    }

    internal void ProcessMessage(
        RedisChannel channel,
        RedisValue message)
    {
        var cacheMessage = JsonSerializer
            .Deserialize(
                message.ToString(),
                SourceGenerationContext.Default.CacheMessage);
        if (cacheMessage != null &&
            cacheMessage.PublisherId != MessagingRedisCacheOptions.Id)
        {
            MemoryCache.Remove(
                cacheMessage.Key);

            OnMessage?.Invoke(
                this,
                new OnMessageEventArgs(cacheMessage.Key));
        }
    }
}
