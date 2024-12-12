using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// A Redis distributed cache implementation that uses pub/sub.
/// </summary>
public class MessagingRedisCache :
    RedisCache,
    IBufferDistributedCache
{
    /// <summary>
    /// Initializes a new instance of MessagingRedisCache.
    /// </summary>
    public MessagingRedisCache(
        IMessagePublisher messagePublisher,
        IMessageSubscriber messageSubscriber,
        IMessagingConfigurationVerifier messagingConfigurationVerifier,
        IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor,
        ILogger<MessagingRedisCache>? logger = null) :
        base(messagingRedisCacheOptionsAccessor)
    {
        Logger = logger ??
            NullLogger<MessagingRedisCache>.Instance;
        MessagePublisher = messagePublisher ??
            throw new ArgumentNullException(
                nameof(messagePublisher));
        MessageSubscriber = messageSubscriber ??
            throw new ArgumentNullException(
                nameof(messageSubscriber));
        MessagingConfigurationVerifier = messagingConfigurationVerifier ??
            throw new ArgumentNullException(
                nameof(messagingRedisCacheOptionsAccessor));
        MessagingRedisCacheOptions = messagingRedisCacheOptionsAccessor?.Value ??
            throw new ArgumentNullException(
                nameof(messagingRedisCacheOptionsAccessor));

        Database = new Lazy<IDatabase>(() =>
            MessagingRedisCacheOptions
                .ConnectionMultiplexerFactory!()
                .GetAwaiter()
                .GetResult()
                .GetDatabase(
                    MessagingRedisCacheOptions
                        .ConfigurationOptions?
                        .DefaultDatabase ?? -1));

        SubscribeCancellationTokenSource = new CancellationTokenSource();
        _ = SubscribeAsync(
            SubscribeCancellationTokenSource.Token);
    }

    /// <summary>
    /// The <c>StackExchange.Redis.IDatabase</c> for the <see cref="RedisCache"/>.
    /// </summary>
    public Lazy<IDatabase> Database { get; }

    /// <summary>
    /// Optional. The logger.
    /// </summary>
    public ILogger<MessagingRedisCache> Logger { get; init; }

    /// <summary>
    /// The pub/sub publisher.
    /// </summary>
    public IMessagePublisher MessagePublisher { get; }

    /// <summary>
    /// The pub/sub subscriber.
    /// </summary>
    public IMessageSubscriber MessageSubscriber { get; }

    /// <summary>
    /// The messaging configuration verifier.
    /// </summary>
    public IMessagingConfigurationVerifier MessagingConfigurationVerifier { get; }

    /// <summary>
    /// Configuration options.
    /// </summary>
    public MessagingRedisCacheOptions MessagingRedisCacheOptions { get; }

    private bool IsDisposed { get; set; }
    private CancellationTokenSource SubscribeCancellationTokenSource { get; set; }

    /// <inheritdoc />
    public new void Dispose()
    {
        base.Dispose();
        Dispose(true);
    }

    /// <inheritdoc />
    public new void Remove(string key)
    {
        base.Remove(key);
        MessagePublisher.Publish(
            Database.Value.Multiplexer,
            key);
    }

    /// <inheritdoc />
    public new async Task RemoveAsync(
        string key,
        CancellationToken token = default)
    {
        await base.RemoveAsync(
            key,
            token)
            .ConfigureAwait(false);
        await MessagePublisher
            .PublishAsync(
                Database.Value.Multiplexer,
                key,
                token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public new void Set(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options)
    {
        base.Set(
            key,
            value,
            options);
        MessagePublisher.Publish(
            Database.Value.Multiplexer,
            key);
    }

    /// <inheritdoc />
    public void Set(
        string key,
        ReadOnlySequence<byte> value,
        DistributedCacheEntryOptions options)
    {
        ((IBufferDistributedCache)this).Set(key, value, options);
        MessagePublisher.Publish(
            Database.Value.Multiplexer,
            key);
    }

    /// <inheritdoc />
    public new async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        await base.SetAsync(
            key,
            value,
            options,
            token)
            .ConfigureAwait(false);
        await MessagePublisher
            .PublishAsync(
                Database.Value.Multiplexer,
                key,
                token)
            .ConfigureAwait(false);
    }

    private void Dispose(bool isDisposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            SubscribeCancellationTokenSource.Dispose();
        }

        IsDisposed = true;
    }

    private async Task SubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            try
            {
                if (!await MessagingConfigurationVerifier
                        .VerifyConfigurationAsync(
                            Database.Value,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    Logger.MessagingConfigurationInvalid(
                        MessagingRedisCacheOptions.MessagingType);
                }

                await MessageSubscriber
                    .SubscribeAsync(
                        Database.Value.Multiplexer,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            catch (RedisConnectionException redisConnectionException)
            {
                Logger.SubscriberFailed(
                    MessagingRedisCacheOptions
                        .SubscriberRetryDelay,
                    redisConnectionException);

                await Task
                    .Delay(
                        MessagingRedisCacheOptions
                            .SubscriberRetryDelay,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
