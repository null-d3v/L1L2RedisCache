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
                LogLevel.Warning,
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

        Subscriber = new Lazy<ISubscriber>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory?
                .Invoke()
                .GetAwaiter()
                .GetResult()
                .GetSubscriber() ??
                throw new InvalidOperationException());
    }

    public IConfigurationVerifier ConfigurationVerifier { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public ILogger<KeyeventMessageSubscriber>? Logger { get; set; }
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Subscribe()
    {
        if (!ConfigurationVerifier
                .TryVerifyConfiguration(
                    "notify-keyspace-events",
                    out var keyeventNotificationsException,
                    "g",
                    "h",
                    "E"))
        {
            if (Logger != null)
            {
                _keyeventNotificationsMisconfigured(
                    Logger,
                    keyeventNotificationsException);
            }
        }

        Subscriber.Value.Subscribe(
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
            });
        Subscriber.Value.Subscribe(
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
            });
    }
}
