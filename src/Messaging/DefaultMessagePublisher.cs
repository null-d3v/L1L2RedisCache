using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal sealed class DefaultMessagePublisher(
    IOptions<JsonSerializerOptions> jsonSerializerOptionsAccessor,
    IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor) :
    IMessagePublisher
{
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        jsonSerializerOptionsAccessor.Value;
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; } =
        l1L2RedisCacheOptionsOptionsAccessor.Value;

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
