using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal sealed class KeyeventMessageSubscriber :
    IMessageSubscriber
{
    private static readonly
        Action<ILogger, Exception?> _keyeventNotificationsMisconfigured =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(0),
                "Failed to verify keyevent notifications config");

    public KeyeventMessageSubscriber(
        IConfigurationVerifier configurationVerifier,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor,
        ILogger<KeyeventMessageSubscriber>? logger = null)
    {
        ConfigurationVerifier = configurationVerifier;
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;

        Logger = logger;
    }

    public IConfigurationVerifier ConfigurationVerifier { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public ILogger<KeyeventMessageSubscriber>? Logger { get; set; }

    public async Task SubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurationVerifier
                .VerifyConfigurationAsync(
                    "notify-keyspace-events",
                    cancellationToken,
                    "g",
                    "h",
                    "E")
                .ConfigureAwait(false))
        {
            if (Logger != null)
            {
                _keyeventNotificationsMisconfigured(
                    Logger,
                    null);
            }
        }

        var subscriber = (await L1L2RedisCacheOptions
            .ConnectionMultiplexerFactory!()
            .ConfigureAwait(false))
            .GetSubscriber();

        await subscriber
            .SubscribeAsync(
                "__keyevent@*__:del",
                (channel, message) =>
                {
                    if (message.StartsWith(
                            L1L2RedisCacheOptions.KeyPrefix))
                    {
                        var key = message
                            .ToString()[L1L2RedisCacheOptions.KeyPrefix.Length..];
                        L1Cache.Remove(
                            $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
                        L1Cache.Remove(
                            $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");
                    }
                })
            .ConfigureAwait(false);

        await subscriber
            .SubscribeAsync(
                "__keyevent@*__:hset",
                (channel, message) =>
                {
                    if (message.StartsWith(
                            L1L2RedisCacheOptions.KeyPrefix))
                    {
                        var key = message
                            .ToString()[L1L2RedisCacheOptions.KeyPrefix.Length..];
                        L1Cache.Remove(
                            $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
                        L1Cache.Remove(
                            $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");
                    }
                })
            .ConfigureAwait(false);
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
                    "__keyevent@*__:del",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false);

        await subscriber
            .UnsubscribeAsync(
                new RedisChannel(
                    "__keyevent@*__:hset",
                    RedisChannel.PatternMode.Pattern))
            .ConfigureAwait(false);
    }
}
