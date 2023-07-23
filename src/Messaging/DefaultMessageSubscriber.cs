using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal sealed class DefaultMessageSubscriber :
    IMessageSubscriber
{
    public DefaultMessageSubscriber(
        IOptions<JsonSerializerOptions> jsonSerializerOptions,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptions.Value;
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }

    public async Task SubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        var subscriber = (await L1L2RedisCacheOptions
            .ConnectionMultiplexerFactory!()
            .ConfigureAwait(false))
            .GetSubscriber();

        (await subscriber
            .SubscribeAsync(
                new RedisChannel(
                    L1L2RedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
            .ConfigureAwait(false))
            .OnMessage(channelMessage =>
            {
                var cacheMessage = JsonSerializer
                    .Deserialize<CacheMessage>(
                        channelMessage.Message.ToString(),
                        JsonSerializerOptions);
                if (cacheMessage?.PublisherId !=
                    L1L2RedisCacheOptions.Id)
                {
                    L1Cache.Remove(
                        $"{L1L2RedisCacheOptions.KeyPrefix}{cacheMessage?.Key}");
                    L1Cache.Remove(
                        $"{L1L2RedisCacheOptions.LockKeyPrefix}{cacheMessage?.Key}");
                }
            });
    }

    public async Task UnsubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        var subscriber = (await L1L2RedisCacheOptions
            .ConnectionMultiplexerFactory!()
            .ConfigureAwait(false))
            .GetSubscriber();

        await subscriber
            .UnsubscribeAsync(
                new RedisChannel(
                    L1L2RedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
            .ConfigureAwait(false);
    }
}
