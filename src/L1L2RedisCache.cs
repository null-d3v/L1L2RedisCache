using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

/// <summary>
/// A distributed cache implementation using both memory and Redis.
/// </summary>
public class L1L2RedisCache : IDistributedCache
{
    /// <summary>
    /// Initializes a new instance of L1L2RedisCache.
    /// </summary>
    public L1L2RedisCache(
        IMemoryCache l1Cache,
        IOptions<L1L2RedisCacheOptions> l1l2RedisCacheOptionsAccessor,
        Func<IDistributedCache> l2CacheAccessor,
        IMessagePublisher messagePublisher,
        IMessageSubscriber messageSubscriber)
    {
        L1Cache = l1Cache;
        L1L2RedisCacheOptions = l1l2RedisCacheOptionsAccessor.Value;
        L2Cache = l2CacheAccessor();
        MessagePublisher = messagePublisher;
        MessageSubscriber = messageSubscriber;

        Database = new Lazy<IDatabase>(() =>
            L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory()
                .GetAwaiter()
                .GetResult()
                .GetDatabase(
                    L1L2RedisCacheOptions
                        .ConfigurationOptions?
                        .DefaultDatabase ?? -1));

        MessageSubscriber.Subscribe();
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
    /// The pub/sub publisher.
    /// </summary>
    public IMessagePublisher MessagePublisher { get; }

    /// <summary>
    /// The pub/sub subscriber.
    /// </summary>
    public IMessageSubscriber MessageSubscriber { get; }

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
                        value,
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
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation, containing the located value or null.</returns>
    public async Task<byte[]?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var value = L1Cache.Get(
            $"{L1L2RedisCacheOptions.KeyPrefix}{key}") as byte[];

        if (value == null)
        {
            if (await Database.Value.KeyExistsAsync(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}"))
            {
                var semaphore = await GetOrCreateLockAsync(
                    key,
                    null,
                    cancellationToken);
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var hashEntries = await GetHashEntriesAsync(key);
                    var distributedCacheEntryOptions = hashEntries
                        .GetDistributedCacheEntryOptions();
                    value = hashEntries.GetRedisValue();

                    SetMemoryCache(
                        key,
                        value,
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
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task RefreshAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await L2Cache.RefreshAsync(key, cancellationToken);
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
            MessagePublisher.Publish(key);
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
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var semaphore = await GetOrCreateLockAsync(
            key, null, cancellationToken);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            L2Cache.Remove(key);
            L1Cache.Remove(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
            MessagePublisher.Publish(key);
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
    /// <param name="distributedCacheEntryOptions">The cache options for the value.</param>
    public void Set(
        string key,
        byte[] value,
        DistributedCacheEntryOptions distributedCacheEntryOptions)
    {
        var semaphore = GetOrCreateLock(
            key, distributedCacheEntryOptions);
        semaphore.Wait();
        try
        {
            L2Cache.Set(
                key, value, distributedCacheEntryOptions);
            SetMemoryCache(
                key, value, distributedCacheEntryOptions);
            MessagePublisher.Publish(key);
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
    /// <param name="distributedCacheEntryOptions">The cache options for the value.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions distributedCacheEntryOptions,
        CancellationToken cancellationToken = default)
    {
        var semaphore = await GetOrCreateLockAsync(
            key, distributedCacheEntryOptions, cancellationToken);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await L2Cache.SetAsync(
                key,
                value,
                distributedCacheEntryOptions,
                cancellationToken);
            SetMemoryCache(
                key, value, distributedCacheEntryOptions);
            await MessagePublisher.PublishAsync(
                key, cancellationToken);
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
            hashEntries = await Database.Value.HashGetAllAsync(
                $"{L1L2RedisCacheOptions.KeyPrefix}{key}");
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
        await KeySemaphore.WaitAsync(cancellationToken);
        try
        {
            return await L1Cache.GetOrCreateAsync(
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
                }) ??
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
}
