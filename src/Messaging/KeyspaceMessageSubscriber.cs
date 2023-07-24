using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal class KeyspaceMessageSubscriber :
    IMessageSubscriber
{
    private static readonly
        Action<ILogger, Exception?> _keyspaceNotificationsMisconfigured =
            LoggerMessage.Define(
                LogLevel.Error,
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
    }

    public IConfigurationVerifier ConfigurationVerifier { get; set; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }
    public IMemoryCache L1Cache { get; set; }
    public ILogger<KeyspaceMessageSubscriber>? Logger { get; set; }
    public EventHandler<OnMessageEventArgs>? OnMessage { get; set; }
    public EventHandler? OnSubscribe { get; set; }

    public async Task SubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurationVerifier
                .VerifyConfigurationAsync(
                    "notify-keyspace-events",
                    cancellationToken,
                    "g",
                    "h",
                    "K")
                .ConfigureAwait(false))
        {
            if (Logger != null)
            {
                _keyspaceNotificationsMisconfigured(
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
        CancellationToken cancellationToken = default)
    {
        var subscriber = (await L1L2RedisCacheOptions
            .ConnectionMultiplexerFactory!()
            .ConfigureAwait(false))
            .GetSubscriber();

        await subscriber
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
