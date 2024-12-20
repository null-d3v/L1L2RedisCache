﻿using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

/// <summary>
/// A distributed cache implementation using both memory and Redis.
/// </summary>
public sealed class L1L2RedisCache :
    IDisposable,
    IDistributedCache
{
    /// <summary>
    /// Initializes a new instance of L1L2RedisCache.
    /// </summary>
    public L1L2RedisCache(
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1l2RedisCacheOptionsAccessor,
        Func<IDistributedCache> l2CacheAccessor,
        IMessagePublisher messagePublisher,
        IMessageSubscriber messageSubscriber,
        IMessagingConfigurationVerifier messagingConfigurationVerifier,
        ILogger<L1L2RedisCache>? logger = null)
    {
        L1Cache = l1Cache ??
            throw new ArgumentNullException(
                nameof(l1Cache));
        L1L2RedisCacheOptions = l1l2RedisCacheOptionsAccessor?.Value ??
            throw new ArgumentNullException(
                nameof(l1l2RedisCacheOptionsAccessor));
        L2Cache = l2CacheAccessor?.Invoke() ??
            throw new ArgumentNullException(
                nameof(l2CacheAccessor));
        Logger = logger ??
            NullLogger<L1L2RedisCache>.Instance;
        MessagePublisher = messagePublisher ??
            throw new ArgumentNullException(
                nameof(messagePublisher));
        MessageSubscriber = messageSubscriber ??
            throw new ArgumentNullException(
                nameof(messageSubscriber));
        MessagingConfigurationVerifier = messagingConfigurationVerifier ??
            throw new ArgumentNullException(
                nameof(l1l2RedisCacheOptionsAccessor));

        Database = new Lazy<IDatabase>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory!()
                .GetAwaiter()
                .GetResult()
                .GetDatabase(
                    L1L2RedisCacheOptions
                        .ConfigurationOptions?
                        .DefaultDatabase ?? -1));

        SubscribeCancellationTokenSource = new CancellationTokenSource();
        _ = SubscribeAsync(
            SubscribeCancellationTokenSource.Token);
    }

    private static SemaphoreSlim KeySemaphore { get; } =
        new SemaphoreSlim(1, 1);

    /// <summary>
    /// The <c>StackExchange.Redis.IDatabase</c> for the <see cref="L2Cache"/>.
    /// </summary>
    public Lazy<IDatabase> Database { get; }

    /// <summary>
    /// The IMemoryCache for L1.
    /// </summary>
    public IMemoryCache L1Cache { get; }

    /// <summary>
    /// Configuration options.
    /// </summary>
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; }

    /// <summary>
    /// The IDistributedCache for L2.
    /// </summary>
    public IDistributedCache L2Cache { get; }

    /// <summary>
    /// Optional. The logger.
    /// </summary>
    public ILogger<L1L2RedisCache> Logger { get; }

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

    private bool IsDisposed { get; set; }
    private CancellationTokenSource SubscribeCancellationTokenSource { get; set; }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets a value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    public byte[]? Get(string key)
    {
        var value = L1Cache.Get(
            $"{L1L2RedisCacheOptions.KeyPrefix}{key}") as byte[];

        if (value == null)
        {
            if (Database.Value.KeyExists(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}"))
            {
                var semaphore = GetOrCreateLock(
                    key,
                    null);
                semaphore.Wait();
                try
                {
                    var hashEntries = GetHashEntries(key);
                    var distributedCacheEntryOptions = hashEntries
                        .GetDistributedCacheEntryOptions();
                    value = hashEntries.GetRedisValue();

                    SetMemoryCache(
                        key,
                        value!,
                        distributedCacheEntryOptions);
                    SetLock(
                        key,
                        semaphore,
                        distributedCacheEntryOptions);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        return value;
    }

    /// <summary>
    /// Gets a value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation, containing the located value or null.</returns>
    public async Task<byte[]?> GetAsync(
        string key,
        CancellationToken token = default)
    {
        var value = L1Cache.Get(
            $"{L1L2RedisCacheOptions.KeyPrefix}{key}") as byte[];

        if (value == null)
        {
            if (await Database.Value
                    .KeyExistsAsync(
                        $"{L1L2RedisCacheOptions.KeyPrefix}{key}")
                    .ConfigureAwait(false))
            {
                var semaphore = await GetOrCreateLockAsync(
                    key,
                    null,
                    token)
                    .ConfigureAwait(false);
                await semaphore
                    .WaitAsync(token)
                    .ConfigureAwait(false);
                try
                {
                    var hashEntries = await GetHashEntriesAsync(key)
                        .ConfigureAwait(false);
                    var distributedCacheEntryOptions = hashEntries
                        .GetDistributedCacheEntryOptions();
                    value = hashEntries.GetRedisValue();

                    SetMemoryCache(
                        key,
                        value!,
                        distributedCacheEntryOptions);
                    SetLock(
                        key,
                        semaphore,
                        distributedCacheEntryOptions);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        return value;
    }

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    public void Refresh(string key)
    {
        L2Cache.Refresh(key);
    }

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task RefreshAsync(
        string key,
        CancellationToken token = default)
    {
        await L2Cache
            .RefreshAsync(key, token)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    public void Remove(string key)
    {
        var semaphore = GetOrCreateLock(key, null);
        semaphore.Wait();
        try
        {
            L2Cache.Remove(key);
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
            MessagePublisher.Publish(
                Database.Value.Multiplexer,
                key);
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Removes the value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task RemoveAsync(
        string key,
        CancellationToken token = default)
    {
        var semaphore = await GetOrCreateLockAsync(
            key,
            null,
            token)
            .ConfigureAwait(false);
        await semaphore
            .WaitAsync(token)
            .ConfigureAwait(false);
        try
        {
            await L2Cache
                .RemoveAsync(key, token)
                .ConfigureAwait(false);
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
            await MessagePublisher
                .PublishAsync(
                    Database.Value.Multiplexer,
                    key,
                    token)
                .ConfigureAwait(false);
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Sets a value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    public void Set(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options)
    {
        var semaphore = GetOrCreateLock(
            key, options);
        semaphore.Wait();
        try
        {
            L2Cache.Set(
                key, value, options);
            SetMemoryCache(
                key, value, options);
            MessagePublisher.Publish(
                Database.Value.Multiplexer,
                key);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Sets a value with the given key.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var semaphore = await GetOrCreateLockAsync(
            key,
            options,
            token)
            .ConfigureAwait(false);
        await semaphore
            .WaitAsync(token)
            .ConfigureAwait(false);
        try
        {
            await L2Cache
                .SetAsync(
                    key,
                    value,
                    options,
                    token)
                .ConfigureAwait(false);
            SetMemoryCache(
                key, value, options);
            await MessagePublisher
                .PublishAsync(
                    Database.Value.Multiplexer,
                    key,
                    token)
                .ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
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

    private HashEntry[] GetHashEntries(string key)
    {
        var hashEntries = Array.Empty<HashEntry>();

        try
        {
            hashEntries = Database.Value.HashGetAll(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
        }
        catch (RedisServerException) { }

        return hashEntries;
    }

    private async Task<HashEntry[]> GetHashEntriesAsync(string key)
    {
        var hashEntries = Array.Empty<HashEntry>();

        try
        {
            hashEntries = await Database.Value
                .HashGetAllAsync(
                    $"{L1L2RedisCacheOptions.KeyPrefix}{key}")
                .ConfigureAwait(false);
        }
        catch (RedisServerException) { }

        return hashEntries;
    }

    private SemaphoreSlim GetOrCreateLock(
        string key,
        DistributedCacheEntryOptions? distributedCacheEntryOptions)
    {
        KeySemaphore.Wait();
        try
        {
            return L1Cache.GetOrCreate(
                $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}",
                cacheEntry =>
                {
                    cacheEntry.AbsoluteExpiration =
                        distributedCacheEntryOptions?.AbsoluteExpiration;
                    cacheEntry.AbsoluteExpirationRelativeToNow =
                        distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
                    cacheEntry.SlidingExpiration =
                        distributedCacheEntryOptions?.SlidingExpiration;
                    return new SemaphoreSlim(1, 1);
                }) ??
                new SemaphoreSlim(1, 1);
        }
        finally
        {
            KeySemaphore.Release();
        }
    }

    private async Task<SemaphoreSlim> GetOrCreateLockAsync(
        string key,
        DistributedCacheEntryOptions? distributedCacheEntryOptions,
        CancellationToken cancellationToken = default)
    {
        await KeySemaphore
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await L1Cache
                .GetOrCreateAsync(
                    $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}",
                    cacheEntry =>
                    {
                        cacheEntry.AbsoluteExpiration =
                            distributedCacheEntryOptions?.AbsoluteExpiration;
                        cacheEntry.AbsoluteExpirationRelativeToNow =
                            distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
                        cacheEntry.SlidingExpiration =
                            distributedCacheEntryOptions?.SlidingExpiration;
                        return Task.FromResult(new SemaphoreSlim(1, 1));
                    })
                .ConfigureAwait(false) ??
                new SemaphoreSlim(1, 1);
        }
        finally
        {
            KeySemaphore.Release();
        }
    }

    private SemaphoreSlim SetLock(
        string key,
        SemaphoreSlim semaphore,
        DistributedCacheEntryOptions distributedCacheEntryOptions)
    {
        var memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration =
                distributedCacheEntryOptions?.AbsoluteExpiration,
            AbsoluteExpirationRelativeToNow =
                distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow,
            SlidingExpiration =
                distributedCacheEntryOptions?.SlidingExpiration,
        };

        return L1Cache.Set(
            $"{L1L2RedisCacheOptions.LockKeyPrefix}{key}",
            semaphore,
            memoryCacheEntryOptions);
    }

    private void SetMemoryCache(
        string key,
        byte[] value,
        DistributedCacheEntryOptions distributedCacheEntryOptions)
    {
        var memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration =
                distributedCacheEntryOptions?.AbsoluteExpiration,
            AbsoluteExpirationRelativeToNow =
                distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow,
            SlidingExpiration =
                distributedCacheEntryOptions?.SlidingExpiration,
        };

        if (!memoryCacheEntryOptions.SlidingExpiration.HasValue)
        {
            L1Cache.Set(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}",
                value,
                memoryCacheEntryOptions);
        }
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
                        L1L2RedisCacheOptions.MessagingType);
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
                    L1L2RedisCacheOptions
                        .SubscriberRetryDelay,
                    redisConnectionException);

                await Task
                    .Delay(
                        L1L2RedisCacheOptions
                            .SubscriberRetryDelay,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
