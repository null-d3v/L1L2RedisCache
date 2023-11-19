using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal class KeyspaceMessageSubscriber(
    IMemoryCache l1Cache,
    IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor) :
    IMessageSubscriber
{
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; } =
        l1L2RedisCacheOptionsOptionsAccessor.Value;
    public IMemoryCache L1Cache { get; set; } =
        l1Cache;
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
                    "__keyspace@*__:*",
                    RedisChannel.PatternMode.Pattern),
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
                    "__keyspace@*__:*",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false);
    }

    internal void ProcessMessage(
        RedisChannel channel,
        RedisValue message)
    {
        if (message == "del" ||
            message == "hset")
        {
            var keyPrefixIndex = channel.ToString().IndexOf(
                L1L2RedisCacheOptions.KeyPrefix,
                StringComparison.Ordinal);
            if (keyPrefixIndex != -1)
            {
                var key = channel.ToString()[
                    (keyPrefixIndex + L1L2RedisCacheOptions.KeyPrefix.Length)..];
                L1Cache.Remove(
                    $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
                L1Cache.Remove(
                    $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");

                OnMessage?.Invoke(
                    this,
                    new OnMessageEventArgs(key));
            }
        }
    }
}
