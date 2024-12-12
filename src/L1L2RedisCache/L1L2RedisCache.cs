using MessagingRedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace L1L2RedisCache;

/// <summary>
/// A distributed cache implementation using both memory and Redis.
/// </summary>
public class L1L2RedisCache(
    IBufferDistributedCache bufferDistributedCache,
    IMemoryCache l1Cache,
    IMessagePublisher messagePublisher,
    IMessageSubscriber messageSubscriber,
    IMessagingConfigurationVerifier messagingConfigurationVerifier,
    IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor,
    ILogger<L1L2RedisCache>? logger = null) :
    MessagingRedisCache.MessagingRedisCache(
        bufferDistributedCache,
        messagePublisher,
        messageSubscriber,
        messagingConfigurationVerifier,
        messagingRedisCacheOptionsAccessor,
        logger),
    IDistributedCache
{
    /// <summary>
    /// The IMemoryCache for L1.
    /// </summary>
    public IMemoryCache L1Cache { get; } =
        l1Cache ??
            throw new ArgumentNullException(
                nameof(l1Cache));

    private SemaphoreSlim KeySemaphore { get; } =
        new SemaphoreSlim(1, 1);

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000")]
    public new byte[]? Get(string key)
    {
        var value = L1Cache.Get(key) as byte[];

        if (value == null)
        {
            if (Database.Value.KeyExists(
                    $"{MessagingRedisCacheOptions.InstanceName}{key}"))
            {
                var semaphore = GetOrCreateKeyLock(
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
                    SetKeyLock(
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

    /// <inheritdoc />
    public new async Task<byte[]?> GetAsync(
        string key,
        CancellationToken token = default)
    {
        var value = L1Cache.Get(key) as byte[];

        if (value == null)
        {
            if (await Database.Value
                    .KeyExistsAsync(
                        $"{MessagingRedisCacheOptions.InstanceName}{key}")
                    .ConfigureAwait(false))
            {
                var semaphore = await GetOrCreateKeyLockAsync(
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
                    SetKeyLock(
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

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000")]
    public new void Remove(string key)
    {
        var semaphore = GetOrCreateKeyLock(key, null);
        semaphore.Wait();
        try
        {
            base.Remove(key);
            L1Cache.Remove(key);
            L1Cache.Remove(
                $"{MessagingRedisCacheOptions.Id}{key}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public new async Task RemoveAsync(
        string key,
        CancellationToken token = default)
    {
        var semaphore = await GetOrCreateKeyLockAsync(
            key,
            null,
            token)
            .ConfigureAwait(false);
        await semaphore
            .WaitAsync(token)
            .ConfigureAwait(false);
        try
        {
            await base
                .RemoveAsync(key, token)
                .ConfigureAwait(false);
            L1Cache.Remove(key);
            L1Cache.Remove(
                $"{MessagingRedisCacheOptions.Id}{key}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000")]
    public new void Set(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options)
    {
        var semaphore = GetOrCreateKeyLock(
            key, options);
        semaphore.Wait();
        try
        {
            base.Set(
                key, value, options);
            SetMemoryCache(
                key, value, options);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public new async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var semaphore = await GetOrCreateKeyLockAsync(
            key,
            options,
            token)
            .ConfigureAwait(false);
        await semaphore
            .WaitAsync(token)
            .ConfigureAwait(false);
        try
        {
            await base
                .SetAsync(
                    key,
                    value,
                    options,
                    token)
                .ConfigureAwait(false);
            SetMemoryCache(
                key, value, options);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private HashEntry[] GetHashEntries(string key)
    {
        var hashEntries = Array.Empty<HashEntry>();

        try
        {
            hashEntries = Database.Value.HashGetAll(
                $"{MessagingRedisCacheOptions.InstanceName}{key}");
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
                    $"{MessagingRedisCacheOptions.InstanceName}{key}")
                .ConfigureAwait(false);
        }
        catch (RedisServerException) { }

        return hashEntries;
    }

    private SemaphoreSlim GetOrCreateKeyLock(
        string key,
        DistributedCacheEntryOptions? distributedCacheEntryOptions)
    {
        KeySemaphore.Wait();
        try
        {
            return L1Cache.GetOrCreate(
                $"{MessagingRedisCacheOptions.Id}{key}",
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

    [SuppressMessage("Reliability", "CA2000")]
    private async Task<SemaphoreSlim> GetOrCreateKeyLockAsync(
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
                    $"{MessagingRedisCacheOptions.Id}{key}",
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

    private SemaphoreSlim SetKeyLock(
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
            $"{MessagingRedisCacheOptions.Id}{key}",
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
                key,
                value,
                memoryCacheEntryOptions);
        }
    }
}