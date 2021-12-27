using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class MessagePublisher : IMessagePublisher
{
    public MessagePublisher(
        IOptions<JsonSerializerOptions> jsonSerializerOptions,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptions.Value;
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
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Publish(string key)
    {
        if (L1L2RedisCacheOptions.MessagingType ==
            MessagingType.Default)
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
    }

    public async Task PublishAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (L1L2RedisCacheOptions.MessagingType ==
            MessagingType.Default)
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
}
