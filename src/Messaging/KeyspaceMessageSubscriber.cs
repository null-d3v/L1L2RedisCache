using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class KeyspaceMessageSubscriber : IMessageSubscriber
{
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
            Logger?.LogWarning(
                keyeventNotificationsException,
                "Failed to verify keyspace notifications config.");
        }

        Subscriber.Value.Subscribe(
            "__keyspace@*__:*",
            (channel, message) =>
            {
                if (message == "del" ||
                    message == "hset")
                {
                    var keyPrefixIndex = channel.ToString().IndexOf(
                        L1L2RedisCacheOptions.KeyPrefix);
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
