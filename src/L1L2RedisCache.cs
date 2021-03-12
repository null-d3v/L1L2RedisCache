using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace L1L2RedisCache
{
    public class L1L2RedisCache : IDistributedCache
    {
        public L1L2RedisCache(
            IConnectionMultiplexer connectionMultiplexer,
            Func<IDistributedCache> distributedCacheAccessor,
            IOptions<JsonSerializerOptions> jsonSerializerOptions,
            IMemoryCache memoryCache,
            IOptions<RedisCacheOptions> redisCacheOptionsAccessor)
        {
            MemoryCache = memoryCache ??
                throw new ArgumentNullException(nameof(memoryCache));
            DistributedCache = distributedCacheAccessor() ??
                throw new ArgumentNullException(nameof(distributedCacheAccessor));
            JsonSerializerOptions = jsonSerializerOptions?.Value ??
                throw new ArgumentNullException(nameof(jsonSerializerOptions));
            RedisCacheOptions = redisCacheOptionsAccessor?.Value ??
                throw new ArgumentNullException(nameof(redisCacheOptionsAccessor));

            Database = connectionMultiplexer?.GetDatabase(
                RedisCacheOptions.ConfigurationOptions?.DefaultDatabase ?? -1) ??
                throw new ArgumentNullException(nameof(connectionMultiplexer));

            KeyPrefix = $"{RedisCacheOptions.InstanceName ?? string.Empty}";
            LockKeyPrefix = $"{Guid.NewGuid()}.{KeyPrefix}";

            Channel = $"{KeyPrefix}Channel";
            PublisherId = Guid.NewGuid();
            Subscriber = connectionMultiplexer.GetSubscriber();
            Subscriber.Subscribe(
                Channel,
                (channel, message) =>
                {
                    var cacheMessage = JsonSerializer
                        .Deserialize<CacheMessage>(
                            message.ToString(),
                            JsonSerializerOptions);
                    if (cacheMessage?.PublisherId != PublisherId)
                    {
                        MemoryCache.Remove(
                            $"{KeyPrefix}{cacheMessage?.Key}");
                        MemoryCache.Remove(
                            $"{LockKeyPrefix}{cacheMessage?.Key}");
                    }
                });
        }

        private static SemaphoreSlim KeySemaphore { get; } =
            new SemaphoreSlim(1, 1);

        public string Channel { get; }
        public IDatabase Database { get; }
        public IDistributedCache DistributedCache { get; }
        public JsonSerializerOptions JsonSerializerOptions { get; }
        public string KeyPrefix { get; }
        public string LockKeyPrefix { get; }
        public IMemoryCache MemoryCache { get; }
        public Guid PublisherId { get; }
        public RedisCacheOptions RedisCacheOptions { get; }
        public ISubscriber Subscriber { get; }

        public byte[] Get(string key)
        {
            var value = MemoryCache.Get(
                $"{KeyPrefix}{key}") as byte[];

            if (value == null)
            {
                if (Database.KeyExists(
                    $"{KeyPrefix}{key}"))
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
                        semaphore.Wait();
                    }
                }
            }

            return value;
        }

        public async Task<byte[]> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var value = MemoryCache.Get(
                $"{KeyPrefix}{key}") as byte[];

            if (value == null)
            {
                if (await Database.KeyExistsAsync(
                    $"{KeyPrefix}{key}"))
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

        public void Refresh(string key)
        {
            DistributedCache.Refresh(key);
        }

        public async Task RefreshAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            await DistributedCache.RefreshAsync(key, cancellationToken);
        }

        public void Remove(string key)
        {
            var semaphore = GetOrCreateLock(key, null);
            semaphore.Wait();
            try
            {
                DistributedCache.Remove(key);
                MemoryCache.Remove(
                    $"{KeyPrefix}{key}");
                Subscriber.Publish(
                    Channel,
                    JsonSerializer.Serialize(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        },
                        JsonSerializerOptions));
                MemoryCache.Remove($"{LockKeyPrefix}{key}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            var semaphore = await GetOrCreateLockAsync(
                key, null, cancellationToken);
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                DistributedCache.Remove(key);
                MemoryCache.Remove(
                    $"{KeyPrefix}{key}");
                Subscriber.Publish(
                    Channel,
                    JsonSerializer.Serialize(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        },
                        JsonSerializerOptions));
                MemoryCache.Remove($"{LockKeyPrefix}{key}");
            }
            finally
            {
                semaphore.Release();
            }
        }

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
                DistributedCache.Set(
                    key, value, distributedCacheEntryOptions);
                SetMemoryCache(
                    key, value, distributedCacheEntryOptions);
                Subscriber.Publish(
                    Channel,
                    JsonSerializer.Serialize(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        },
                        JsonSerializerOptions));
            }
            finally
            {
                semaphore.Release();
            }
        }

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
                await DistributedCache.SetAsync(
                    key,
                    value,
                    distributedCacheEntryOptions,
                    cancellationToken);
                SetMemoryCache(
                    key, value, distributedCacheEntryOptions);
                await Subscriber.PublishAsync(
                    Channel,
                    JsonSerializer.Serialize(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        },
                        JsonSerializerOptions));
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
                hashEntries = Database.HashGetAll(
                    $"{KeyPrefix}{key}");
            }
            catch (RedisServerException) { }

            return hashEntries;
        }

        private async Task<HashEntry[]> GetHashEntriesAsync(string key)
        {
            var hashEntries = Array.Empty<HashEntry>();

            try
            {
                hashEntries = await Database.HashGetAllAsync(
                    $"{KeyPrefix}{key}");
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
                return MemoryCache.GetOrCreate(
                    $"{LockKeyPrefix}{key}",
                    cacheEntry =>
                    {
                        cacheEntry.AbsoluteExpiration =
                            distributedCacheEntryOptions?.AbsoluteExpiration;
                        cacheEntry.AbsoluteExpirationRelativeToNow =
                            distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
                        cacheEntry.SlidingExpiration =
                            distributedCacheEntryOptions?.SlidingExpiration;
                        return new SemaphoreSlim(0, 1);
                    }) ??
                    new SemaphoreSlim(0, 1);
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
                return await MemoryCache.GetOrCreateAsync(
                    $"{LockKeyPrefix}{key}",
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

            return MemoryCache.Set(
                $"{LockKeyPrefix}{key}",
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
                MemoryCache.Set(
                    $"{KeyPrefix}{key}",
                    value,
                    memoryCacheEntryOptions);
            }
        }
    }
}
