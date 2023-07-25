using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class DefaultMessageSubscriber :
    IMessageSubscriber
{
    public DefaultMessageSubscriber(
        IOptions<JsonSerializerOptions> jsonSerializerOptionsAcccessor,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        JsonSerializerOptions = jsonSerializerOptionsAcccessor.Value;
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
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
                    L1L2RedisCacheOptions.Channel,
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
                    L1L2RedisCacheOptions.Channel,
                    RedisChannel.PatternMode.Literal))
            .ConfigureAwait(false);
    }

    internal void ProcessMessage(
        RedisChannel channel,
        RedisValue message)
    {
        var cacheMessage = JsonSerializer
            .Deserialize<CacheMessage>(
                message.ToString(),
                JsonSerializerOptions);
        if (cacheMessage != null &&
            cacheMessage.PublisherId != L1L2RedisCacheOptions.Id)
        {
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{cacheMessage.Key}");
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{cacheMessage.Key}");

            OnMessage?.Invoke(
                this,
                new OnMessageEventArgs(cacheMessage.Key));
        }
    }
}
