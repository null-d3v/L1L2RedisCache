using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class DefaultMessageSubscriber : IMessageSubscriber
{
    public DefaultMessageSubscriber(
        IOptions<JsonSerializerOptions> jsonSerializerOptions,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptions.Value;
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;

        Subscriber = new Lazy<ISubscriber>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory()
                .GetAwaiter()
                .GetResult()
                .GetSubscriber());
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Subscribe()
    {
        Subscriber.Value.Subscribe(
            L1L2RedisCacheOptions.Channel,
            (channel, message) =>
            {
                var cacheMessage = JsonSerializer
                    .Deserialize<CacheMessage>(
                        message.ToString(),
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
}
