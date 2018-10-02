using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace L1L2RedisCache
{
    public class L1L2RedisCache : IDistributedCache
    {
        private const string AbsoluteExpirationKey = "absexp";
        private const long NotPresent = -1;
        private const string SlidingExpirationKey = "sldexp";

        public L1L2RedisCache(
            IConnectionMultiplexer connectionMultiplexer,
            IMemoryCache memoryCache,
            RedisCache redisCache,
            IOptions<RedisCacheOptions> redisCacheOptionsAccessor)
        {
            ConnectionMultiplexer = connectionMultiplexer ??
                throw new ArgumentNullException(nameof(connectionMultiplexer));
            MemoryCache = memoryCache ??
                throw new ArgumentNullException(nameof(memoryCache));
            RedisCache = redisCache ??
                throw new ArgumentNullException(nameof(redisCache));
            RedisCacheOptions = redisCacheOptionsAccessor?.Value ??
                throw new ArgumentNullException(nameof(redisCacheOptionsAccessor));

            Channel = $"{RedisCacheOptions.InstanceName}.Channel";
            PublisherId = Guid.NewGuid();
            Subscriber = ConnectionMultiplexer.GetSubscriber();
            Subscriber.Subscribe(
                Channel,
                (channel, message) =>
                {
                    var cacheMessage = JsonConvert
                        .DeserializeObject<CacheMessage>(message.ToString());
                    if (cacheMessage.PublisherId != PublisherId)
                    {
                        MemoryCache.Remove(
                            $"{RedisCacheOptions.InstanceName}{cacheMessage.Key}");
                    }
                });
        }

        public string Channel { get; }
        public IConnectionMultiplexer ConnectionMultiplexer { get; }
        public IMemoryCache MemoryCache { get; }
        public Guid PublisherId { get; }
        public RedisCache RedisCache { get; }
        public RedisCacheOptions RedisCacheOptions { get; }
        public ISubscriber Subscriber { get; }

        public byte[] Get(string key)
        {
            var value = MemoryCache.Get(
                $"{RedisCacheOptions.InstanceName}{key}") as byte[];

            if (value == null)
            {
                value = RedisCache.Get(key);
                if (value != null)
                {
                    SetMemoryCache(key, value);
                }
            }

            return value;
        }

        public async Task<byte[]> GetAsync(
            string key,
            CancellationToken token = default(CancellationToken))
        {
            var value = MemoryCache.Get(
                $"{RedisCacheOptions.InstanceName}{key}") as byte[];

            if (value == null)
            {
                value = await RedisCache.GetAsync(key, token);
                if (value != null)
                {
                    SetMemoryCache(key, value);
                }
            }

            return value;
        }

        public void Refresh(string key)
        {
            RedisCache.Refresh(key);
        }

        public async Task RefreshAsync(
            string key,
            CancellationToken token = default(CancellationToken))
        {
            await RedisCache.RefreshAsync(key, token);
        }

        public void Remove(string key)
        {
            RedisCache.Remove(key);
            MemoryCache.Remove(
                $"{RedisCacheOptions.InstanceName}{key}");
            Subscriber.Publish(
                Channel,
                JsonConvert.SerializeObject(
                    new CacheMessage
                    {
                        Key = key,
                        PublisherId = PublisherId,
                    }));
        }

        public async Task RemoveAsync(
            string key,
            CancellationToken token = default(CancellationToken))
        {
            await RedisCache.RemoveAsync(key);
            MemoryCache.Remove(
                $"{RedisCacheOptions.InstanceName}{key}");
            await Subscriber.PublishAsync(Channel, key);
        }

        public void Set(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options)
        {
            RedisCache.Set(key, value, options);
            SetMemoryCache(key, value, options);
            Subscriber.Publish(
                Channel,
                JsonConvert.SerializeObject(
                    new CacheMessage
                    {
                        Key = key,
                        PublisherId = PublisherId,
                    }));
        }

        public async Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default(CancellationToken))
        {
            await RedisCache.SetAsync(key, value, options);
            SetMemoryCache(key, value, options);
            await Subscriber.PublishAsync(
                Channel,
                JsonConvert.SerializeObject(
                    new CacheMessage
                    {
                        Key = key,
                        PublisherId = PublisherId,
                    }));
        }

        private void SetMemoryCache(
            string key,
            byte[] value,
            DistributedCacheEntryOptions distributedCacheEntryOptions = null)
        {
            var memoryCacheEntryOptions = new MemoryCacheEntryOptions();
            
            if (distributedCacheEntryOptions != null)
            {
                memoryCacheEntryOptions.AbsoluteExpiration =
                    distributedCacheEntryOptions.AbsoluteExpiration;
                memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow =
                    distributedCacheEntryOptions.AbsoluteExpirationRelativeToNow;
                memoryCacheEntryOptions.SlidingExpiration =
                    distributedCacheEntryOptions.SlidingExpiration;
            }
            else
            {
                var hashEntries = ConnectionMultiplexer
                    .GetDatabase()
                    .HashGetAll(key);

                var absoluteExpirationHashEntry = hashEntries.FirstOrDefault(
                    hashEntry => hashEntry.Name == AbsoluteExpirationKey);
                if (absoluteExpirationHashEntry != null &&
                    absoluteExpirationHashEntry.Value.HasValue &&
                    absoluteExpirationHashEntry.Value != NotPresent)
                {
                    memoryCacheEntryOptions.AbsoluteExpiration = new DateTimeOffset(
                        (long)absoluteExpirationHashEntry.Value, TimeSpan.Zero);
                }

                var slidingExpirationHashEntry = hashEntries.FirstOrDefault(
                    hashEntry => hashEntry.Name == SlidingExpirationKey);
                if (slidingExpirationHashEntry != null &&
                    slidingExpirationHashEntry.Value.HasValue &&
                    slidingExpirationHashEntry.Value != NotPresent)
                {
                    memoryCacheEntryOptions.SlidingExpiration = new TimeSpan(
                        (long)slidingExpirationHashEntry.Value);
                }
            }

            if (!memoryCacheEntryOptions.SlidingExpiration.HasValue)
            {
                MemoryCache.Set(
                    $"{RedisCacheOptions.InstanceName}{key}",
                    value,
                    memoryCacheEntryOptions);
            }
        }
    }
}
