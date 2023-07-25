using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal sealed class DefaultMessagePublisher :
    IMessagePublisher
{
    public DefaultMessagePublisher(
        IOptions<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptionsAccessor.Value;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }

    public void Publish(
        IConnectionMultiplexer connectionMultiplexer,
        string key)
    {
        connectionMultiplexer
            .GetSubscriber()
            .Publish(
            new RedisChannel(
                L1L2RedisCacheOptions.Channel,
                RedisChannel.PatternMode.Literal),
            JsonSerializer.Serialize(
                new CacheMessage
                {
                    Key = key,
                    PublisherId = L1L2RedisCacheOptions.Id,
                },
                JsonSerializerOptions));
    }

    public async Task PublishAsync(
        IConnectionMultiplexer connectionMultiplexer,
        string key,
        CancellationToken cancellationToken = default)
    {
        await connectionMultiplexer
            .GetSubscriber()
            .PublishAsync(
                new RedisChannel(
                    L1L2RedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal),
                JsonSerializer.Serialize(
                    new CacheMessage
                    {
                        Key = key,
                        PublisherId = L1L2RedisCacheOptions.Id,
                    },
                    JsonSerializerOptions))
            .ConfigureAwait(false);
    }
}
