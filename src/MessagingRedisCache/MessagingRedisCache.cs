using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Buffers;

namespace MessagingRedisCache;

/// <summary>
/// A Redis distributed cache implementation that uses pub/sub.
/// </summary>
public class MessagingRedisCache :
    IBufferDistributedCache,
    IDisposable
{
    /// <summary>
    /// Initializes a new instance of MessagingRedisCache.
    /// </summary>
    public MessagingRedisCache(
        IBufferDistributedCache bufferDistributedCache,
        IMessagePublisher messagePublisher,
        IMessageSubscriber messageSubscriber,
        IMessagingConfigurationVerifier messagingConfigurationVerifier,
        IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor,
        ILogger<MessagingRedisCache>? logger = null)
    {
        BufferDistributedCache = bufferDistributedCache ??
            throw new ArgumentNullException(
                nameof(bufferDistributedCache));
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
    /// The backing <c>IBufferDistributedCache</c>, implemented by <see cref="RedisCache"/>.
    /// </summary>
    public IBufferDistributedCache BufferDistributedCache { get; }

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
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        return BufferDistributedCache
            .Get(key);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(
        string key,
        CancellationToken token = default)
    {
        return await BufferDistributedCache
            .GetAsync(
                key,
                token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        BufferDistributedCache
            .Refresh(key);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(
        string key,
        CancellationToken token = default)
    {
        await BufferDistributedCache
            .RefreshAsync(
                key,
                token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        BufferDistributedCache
            .Remove(key);
        MessagePublisher.Publish(
            Database.Value.Multiplexer,
            key);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        string key,
        CancellationToken token = default)
    {
        await BufferDistributedCache
            .RemoveAsync(
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
    public void Set(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options)
    {
        BufferDistributedCache.Set(
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
        BufferDistributedCache.Set(
            key,
            value,
            options);
        MessagePublisher.Publish(
            Database.Value.Multiplexer,
            key);
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        await BufferDistributedCache
            .SetAsync(
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

    public async ValueTask SetAsync(
        string key,
        ReadOnlySequence<byte> value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        await BufferDistributedCache
            .SetAsync(
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

    public bool TryGet(
        string key,
        IBufferWriter<byte> destination)
    {
        return BufferDistributedCache
            .TryGet(
                key,
                destination);
    }

    public async ValueTask<bool> TryGetAsync(
        string key,
        IBufferWriter<byte> destination,
        CancellationToken token = default)
    {
        return await BufferDistributedCache
            .TryGetAsync(
                key,
                destination,
                token)
            .ConfigureAwait(false);
    }

    protected virtual void Dispose(bool isDisposing)
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
