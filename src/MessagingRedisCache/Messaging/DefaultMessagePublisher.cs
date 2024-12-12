using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace MessagingRedisCache;

internal sealed class DefaultMessagePublisher(
    IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor) :
    IMessagePublisher
{
    public MessagingRedisCacheOptions MessagingRedisCacheOptions { get; set; } =
        messagingRedisCacheOptionsAccessor.Value;

    public void Publish(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key)
    {
        connectionMultiplexer
            .GetSubscriber()
            .Publish(
                new RedisChannel(
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal),
                JsonSerializer.Serialize(
                    new CacheMessage
                    {
                        Key = key.ToString(),
                        PublisherId = MessagingRedisCacheOptions.Id,
                    },
                    SourceGenerationContext.Default.CacheMessage));
    }

    public async Task PublishAsync(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key,
        CancellationToken cancellationToken = default)
    {
        await connectionMultiplexer
            .GetSubscriber()
            .PublishAsync(
                new RedisChannel(
                    MessagingRedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal),
                JsonSerializer.Serialize(
                    new CacheMessage
                    {
                        Key = key.ToString(),
                        PublisherId = MessagingRedisCacheOptions.Id,
                    },
                    SourceGenerationContext.Default.CacheMessage))
            .ConfigureAwait(false);
    }
}
