using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class DefaultMessagePublisher : IMessagePublisher
{
    public DefaultMessagePublisher(
        IOptions<JsonSerializerOptions> jsonSerializerOptions,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptions.Value;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;

        Subscriber = new Lazy<ISubscriber>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory?
                .Invoke()
                .GetAwaiter()
                .GetResult()
                .GetSubscriber() ??
                throw new InvalidOperationException());
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Publish(string key)
    {
        Subscriber.Value.Publish(
            L1L2RedisCacheOptions.Channel,
            JsonSerializer.Serialize(
                new CacheMessage
                {
                    Key = key,
                    PublisherId = L1L2RedisCacheOptions.Id,
                },
                JsonSerializerOptions));
    }

    public async Task PublishAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await Subscriber.Value.PublishAsync(
            L1L2RedisCacheOptions.Channel,
            JsonSerializer.Serialize(
                new CacheMessage
                {
                    Key = key,
                    PublisherId = L1L2RedisCacheOptions.Id,
                },
                JsonSerializerOptions));
    }
}
