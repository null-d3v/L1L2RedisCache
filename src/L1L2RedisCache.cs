using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace L1L2RedisCache
{
    public class L1L2RedisCache : IDistributedCache
    {
        public L1L2RedisCache(
            IConnectionMultiplexer connectionMultiplexer,
            IMemoryCache memoryCache,
            Func<IDistributedCache> distributedCacheAccessor,
            IOptions<RedisCacheOptions> redisCacheOptionsAccessor)
        {
            MemoryCache = memoryCache ??
                throw new ArgumentNullException(nameof(memoryCache));
            DistributedCache = distributedCacheAccessor() ??
                throw new ArgumentNullException(nameof(distributedCacheAccessor));
            RedisCacheOptions = redisCacheOptionsAccessor?.Value ??
                throw new ArgumentNullException(nameof(redisCacheOptionsAccessor));

            Database = connectionMultiplexer?.GetDatabase(
                RedisCacheOptions.ConfigurationOptions?.DefaultDatabase ?? -1) ??
                throw new ArgumentNullException(nameof(connectionMultiplexer));

            KeyPrefix = $"{RedisCacheOptions.InstanceName ?? string.Empty}";
            LockKeyPrefix = $"{Guid.NewGuid().ToString()}.{KeyPrefix}";

            Channel = $"{KeyPrefix}Channel";
            PublisherId = Guid.NewGuid();
            Subscriber = connectionMultiplexer.GetSubscriber();
            Subscriber.Subscribe(
                Channel,
                (channel, message) =>
                {
                    var cacheMessage = JsonConvert
                        .DeserializeObject<CacheMessage>(message.ToString());
                    if (cacheMessage.PublisherId != PublisherId)
                    {
                        MemoryCache.Remove(
                            $"{KeyPrefix}{cacheMessage.Key}");
                        MemoryCache.Remove(
                            $"{LockKeyPrefix}{cacheMessage.Key}");
                    }
                });
        }

        private static object LockKeyLock { get; } = new object();

        public string Channel { get; }
        public IDatabase Database { get; }
        public IDistributedCache DistributedCache { get; }
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
                    var localLock = GetOrCreateLock(
                        key,
                        null);
                    lock (localLock)
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
                            localLock,
                            distributedCacheEntryOptions);
                    }
                }
            }

            return value;
        }

        public async Task<byte[]> GetAsync(
            string key,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var value = MemoryCache.Get(
                $"{KeyPrefix}{key}") as byte[];

            if (value == null)
            {
                if (await Database.KeyExistsAsync(
                    $"{KeyPrefix}{key}"))
                {
                    var localLock = await GetOrCreateLockAsync(
                        key,
                        null);
                    lock (localLock)
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
                            localLock,
                            distributedCacheEntryOptions);
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
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await DistributedCache.RefreshAsync(key, cancellationToken);
        }

        public void Remove(string key)
        {
            lock (GetOrCreateLock(key, null))
            {
                DistributedCache.Remove(key);
                MemoryCache.Remove(
                    $"{KeyPrefix}{key}");
                Subscriber.Publish(
                    Channel,
                    JsonConvert.SerializeObject(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        }));
                MemoryCache.Remove($"{LockKeyPrefix}{key}");
            }
        }

        public async Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (await GetOrCreateLockAsync(
                key, null, cancellationToken))
            {
                DistributedCache.Remove(key);
                MemoryCache.Remove(
                    $"{KeyPrefix}{key}");
                Subscriber.Publish(
                    Channel,
                    JsonConvert.SerializeObject(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        }));
                MemoryCache.Remove($"{LockKeyPrefix}{key}");
            }
        }

        public void Set(
            string key,
            byte[] value,
            DistributedCacheEntryOptions distributedCacheEntryOptions)
        {
            lock (GetOrCreateLock(key, distributedCacheEntryOptions))
            {
                DistributedCache.Set(
                    key, value, distributedCacheEntryOptions);
                SetMemoryCache(
                    key, value, distributedCacheEntryOptions);
                Subscriber.Publish(
                    Channel,
                    JsonConvert.SerializeObject(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        }));
            }
        }

        public async Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions distributedCacheEntryOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (await GetOrCreateLockAsync(
                key, distributedCacheEntryOptions, cancellationToken))
            {
                DistributedCache.Set(
                    key, value, distributedCacheEntryOptions);
                SetMemoryCache(
                    key, value, distributedCacheEntryOptions);
                Subscriber.Publish(
                    Channel,
                    JsonConvert.SerializeObject(
                        new CacheMessage
                        {
                            Key = key,
                            PublisherId = PublisherId,
                        }));
            }
        }

        private HashEntry[] GetHashEntries(string key)
        {
            var hashEntries = new HashEntry[] { };

            try
            {
                hashEntries = Database.HashGetAll(
                    $"{KeyPrefix}{key}");
            }
            catch (RedisServerException) { }

            return hashEntries;
        }

        private object GetOrCreateLock(
            string key,
            DistributedCacheEntryOptions distributedCacheEntryOptions)
        {
            lock (LockKeyLock)
            {
                return MemoryCache.GetOrCreate(
                    $"{LockKeyPrefix}{key}",
                    cacheEntry =>
                    {
                        cacheEntry.AbsoluteExpiration =
                            distributedCacheEntryOptions.AbsoluteExpiration;
                        cacheEntry.AbsoluteExpirationRelativeToNow =
                            distributedCacheEntryOptions.AbsoluteExpirationRelativeToNow;
                        cacheEntry.SlidingExpiration =
                            distributedCacheEntryOptions.SlidingExpiration;
                        return Task.FromResult(new object());
                    }) ??
                    new object();
            }
        }

        private Task<object> GetOrCreateLockAsync(
            string key,
            DistributedCacheEntryOptions distributedCacheEntryOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (LockKeyLock)
            {
                return Task.FromResult(MemoryCache.GetOrCreate<object>(
                    $"{LockKeyPrefix}{key}",
                    cacheEntry =>
                    {
                        cacheEntry.AbsoluteExpiration =
                            distributedCacheEntryOptions?.AbsoluteExpiration;
                        cacheEntry.AbsoluteExpirationRelativeToNow =
                            distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
                        cacheEntry.SlidingExpiration =
                            distributedCacheEntryOptions?.SlidingExpiration;
                        return Task.FromResult(new object());
                    }) ??
                    new object());
            }
        }

        private object SetLock(
            string key,
            object value,
            DistributedCacheEntryOptions distributedCacheEntryOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var memoryCacheEntryOptions = new MemoryCacheEntryOptions();

            memoryCacheEntryOptions.AbsoluteExpiration =
                distributedCacheEntryOptions?.AbsoluteExpiration;
            memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow =
                distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
            memoryCacheEntryOptions.SlidingExpiration =
                distributedCacheEntryOptions?.SlidingExpiration;

            return MemoryCache.Set(
                $"{LockKeyPrefix}{key}",
                value,
                memoryCacheEntryOptions);
        }

        private void SetMemoryCache(
            string key,
            byte[] value,
            DistributedCacheEntryOptions distributedCacheEntryOptions)
        {
            var memoryCacheEntryOptions = new MemoryCacheEntryOptions();

            memoryCacheEntryOptions.AbsoluteExpiration =
                distributedCacheEntryOptions?.AbsoluteExpiration;
            memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow =
                distributedCacheEntryOptions?.AbsoluteExpirationRelativeToNow;
            memoryCacheEntryOptions.SlidingExpiration =
                distributedCacheEntryOptions?.SlidingExpiration;

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
