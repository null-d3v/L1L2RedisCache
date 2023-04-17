using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal sealed class KeyspaceMessageSubscriber :
    IMessageSubscriber
{
    private static readonly
        Action<ILogger, Exception?> _keyspaceNotificationsMisconfigured =
            LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(0),
                "Failed to verify keyspace notifications config");

    public KeyspaceMessageSubscriber(
        IConfigurationVerifier configurationVerifier,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor,
        ILogger<KeyspaceMessageSubscriber>? logger = null)
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
    public ILogger<KeyspaceMessageSubscriber>? Logger { get; set; }
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Subscribe()
    {
        if (!ConfigurationVerifier
                .TryVerifyConfiguration(
                    "notify-keyspace-events",
                    out var keyeventNotificationsException,
                    "g",
                    "h",
                    "K"))
        {
            if (Logger != null)
            {
                _keyspaceNotificationsMisconfigured(
                    Logger,
                    keyeventNotificationsException);
            }
        }

        Subscriber.Value.Subscribe(
            "__keyspace@*__:*",
            (channel, message) =>
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
                    }
                }
            });
    }
}
