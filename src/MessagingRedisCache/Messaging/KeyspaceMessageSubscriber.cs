using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.RegularExpressions;

namespace MessagingRedisCache;

internal class KeyspaceMessageSubscriber(
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

    private Regex ChannelPrefixRegex { get; } = new(
        @$"__keyspace@\d*__:{messagingRedisCacheOptionsAccessor.Value.InstanceName}");

    public async Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    $"__keyspace@*__:{MessagingRedisCacheOptions.InstanceName}*",
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
                    $"__keyspace@*__:{MessagingRedisCacheOptions.InstanceName}*",
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
            var key = ChannelPrefixRegex.Replace(
                channel.ToString(),
                string.Empty);
            MemoryCache.Remove(
                key);

            OnMessage?.Invoke(
                this,
                new OnMessageEventArgs(key));
        }
    }
}
