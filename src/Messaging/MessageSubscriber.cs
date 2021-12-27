using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace L1L2RedisCache;

internal class MessageSubscriber : IMessageSubscriber
{
    public MessageSubscriber(
        IOptions<JsonSerializerOptions> jsonSerializerOptions,
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor,
        ILogger<MessageSubscriber>? logger = null)
    {
        JsonSerializerOptions = jsonSerializerOptions.Value;
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;

        Logger = logger;

        Subscriber = new Lazy<ISubscriber>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory()
                .GetAwaiter()
                .GetResult()
                .GetSubscriber());
    }

    public JsonSerializerOptions JsonSerializerOptions { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public ILogger<MessageSubscriber>? Logger { get; set; }
    public Lazy<ISubscriber> Subscriber { get; set; }

    public void Subscribe()
    {
        switch (L1L2RedisCacheOptions.MessagingType)
        {
            case MessagingType.KeyeventNotifications:
                if (!TryVerifyConfig(
                    "notify-keyspace-events",
                    out var keyeventNotificationsException,
                    "g",
                    "h",
                    "E"))
                {
                    Logger?.LogWarning(
                        keyeventNotificationsException,
                        "Failed to verify keyevent notifications config.");
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
                            RemoveL1Values(key);
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
                            RemoveL1Values(key);
                        }
                    });
                break;

            case MessagingType.KeyspaceNotifications:
                if (!TryVerifyConfig(
                    "notify-keyspace-events",
                    out var keyspaceNotificationsException,
                    "g",
                    "h",
                    "K"))
                {
                    Logger?.LogWarning(
                        keyspaceNotificationsException,
                        "Failed to verify keyevent notifications config.");
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
                                RemoveL1Values(channel.ToString()[
                                    (keyPrefixIndex + L1L2RedisCacheOptions.KeyPrefix.Length)..]);
                            }
                        }
                    });
                break;

            case MessagingType.Default:
            default:
                Subscriber.Value.Subscribe(
                    L1L2RedisCacheOptions.Channel,
                    (channel, message) =>
                    {
                        var cacheMessage = JsonSerializer
                            .Deserialize<CacheMessage>(
                                message.ToString(),
                                JsonSerializerOptions);
                        if (cacheMessage?.PublisherId !=
                            L1L2RedisCacheOptions.Id)
                        {
                            RemoveL1Values(cacheMessage?.Key);
                        }
                    });
                break;
        }
    }

    private void RemoveL1Values(
        string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");
        }
    }

    private bool TryVerifyConfig(
        string config,
        out Exception? error,
        params string[] expectedValues)
    {
        error = null;
        var verified = true;

        try
        {
            var database = L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory()
                .GetAwaiter()
                .GetResult()
                .GetDatabase(
                    L1L2RedisCacheOptions
                        .ConfigurationOptions?
                        .DefaultDatabase ?? -1);

            var configValue = database
                .Execute(
                    "config",
                    "get",
                    config)
                .ToDictionary()[config]
                .ToString();
            foreach (var expectedValue in expectedValues)
            {
                if (configValue?.Contains(expectedValue) != true)
                {
                    verified = false;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception;
            verified = false;
        }

        return verified;
    }
}
