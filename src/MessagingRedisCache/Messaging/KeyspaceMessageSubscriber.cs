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

    private Regex ChannelPrefixRegex { get; } = new(
        @$"__keyspace@\d*__:{messagingRedisCacheOptionsAccessor.Value.InstanceName}");

    public async Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        (await connectionMultiplexer
            .GetSubscriber()
            .SubscribeAsync(
                new RedisChannel(
                    $"__keyspace@*__:{MessagingRedisCacheOptions.InstanceName}*",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false))
            .OnMessage(ProcessMessageAsync);

        await MessagingRedisCacheOptions
            .Events
            .OnSubscribe
            .Invoke()
            .ConfigureAwait(false);
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

    internal async Task ProcessMessageAsync(
        ChannelMessage channelMessage)
    {
        if (channelMessage.Message == "del" ||
            channelMessage.Message == "hset")
        {
            var key = ChannelPrefixRegex.Replace(
                channelMessage.Channel.ToString(),
                string.Empty);
            MemoryCache.Remove(
                key);

            await MessagingRedisCacheOptions
                .Events
                .OnMessageRecieved
                .Invoke(channelMessage)
                .ConfigureAwait(false);
        }
    }
}
