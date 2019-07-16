using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace L1L2RedisCache.Test.Unit
{
    public class L1L2RedisCacheTest
    {
        public L1L2RedisCacheTest()
        {
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();

            var mockDatabase = new Mock<IDatabase>();
            mockDatabase
                .Setup(
                    d => d.HashGetAll(
                        It.IsAny<RedisKey>(),
                        It.IsAny<CommandFlags>()))
                .Returns(new HashEntry[] { });
            mockDatabase
                .Setup(
                    d => d.KeyExists(
                        It.IsAny<RedisKey>(),
                        It.IsAny<CommandFlags>()))
                .Returns<RedisKey, CommandFlags>(
                    (k, cF) => L2Cache.Get(k) != null);
            mockDatabase
                .Setup(
                    d => d.KeyExistsAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<CommandFlags>()))
                .Returns<RedisKey, CommandFlags>(
                    async (k, cF) =>
                    {
                        var key  = ((string)k).Substring(
                            RedisCacheOptions.InstanceName?.Length ?? 0);
                        return await L2Cache.GetAsync(key) != null;
                    });

            var mockSubscriber = new Mock<ISubscriber>();
            mockSubscriber
                .Setup(
                    s => s.Subscribe(
                        It.IsAny<RedisChannel>(),
                        It.IsAny<Action<RedisChannel, RedisValue>>(),
                        It.IsAny<CommandFlags>()));

            mockConnectionMultiplexer
                .Setup(cM => cM.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            mockConnectionMultiplexer
                .Setup(cM => cM.GetSubscriber(It.IsAny<object>()))
                .Returns(mockSubscriber.Object);

            L1Cache = new MemoryCache(
                Options.Create<MemoryCacheOptions>(
                    new MemoryCacheOptions()));

            L2Cache = new MemoryDistributedCache(
                Options.Create<MemoryDistributedCacheOptions>(
                    new MemoryDistributedCacheOptions()));

            var redisCacheOptionsAccessor = Options.Create<RedisCacheOptions>(
                new RedisCacheOptions
                {
                    InstanceName = "InstanceName",
                });
            RedisCacheOptions = redisCacheOptionsAccessor.Value;

            L1L2Cache = new L1L2RedisCache(
                mockConnectionMultiplexer.Object,
                L1Cache,
                new Func<IDistributedCache>(() => L2Cache),
                RedisCacheOptions);
        }

        public IMemoryCache L1Cache { get; }
        public IDistributedCache L1L2Cache { get; }
        public IDistributedCache L2Cache { get; }
        public RedisCacheOptions RedisCacheOptions { get; }

        [Fact]
        public async Task L1Propagation()
        {
            var key = "key";
            var value = new byte[] { 0x20, 0x20, 0x20, };

            var memoryKey = $"{RedisCacheOptions.InstanceName}{key}";

            await L2Cache.SetAsync(key, value);

            Assert.Null(
                L1Cache.Get(memoryKey));

            await L1L2Cache.GetAsync(key);

            Assert.Equal(
                value,
                L1Cache.Get(memoryKey));
        }

        [Fact]
        public void Set()
        {
            var key = "key";
            var value = new byte[] { 0x20, 0x20, 0x20, };

            var memoryKey = $"{RedisCacheOptions.InstanceName}{key}";

            L1L2Cache.Set(key, value);

            Assert.Equal(
                value,
                L1L2Cache.Get(key));
            Assert.Equal(
                value,
                L1Cache.Get(memoryKey));
            Assert.Equal(
                value,
                L2Cache.Get(key));
        }

        [Fact]
        public void Set_Remove()
        {
            var key = "key";
            var value = new byte[] { 0x20, 0x20, 0x20, };

            var memoryKey = $"{RedisCacheOptions.InstanceName}{key}";

            L1L2Cache.Set(key, value);

            Assert.Equal(
                value,
                L1L2Cache.Get(key));
            Assert.Equal(
                value,
                L1Cache.Get(memoryKey));
            Assert.Equal(
                value,
                L2Cache.Get(key));

            L1L2Cache.Remove(key);

            Assert.Null(
                L1L2Cache.Get(key));
            Assert.Null(
                L1Cache.Get(memoryKey));
            Assert.Null(
                L2Cache.Get(key));
        }

        [Fact]
        public async Task SetAsync()
        {
            var key = "key";
            var value = new byte[] { 0x20, 0x20, 0x20, };

            var memoryKey = $"{RedisCacheOptions.InstanceName}{key}";

            await L1L2Cache.SetAsync(key, value);

            Assert.Equal(
                value,
                await L1L2Cache.GetAsync(key));
            Assert.Equal(
                value,
                L1Cache.Get(memoryKey));
            Assert.Equal(
                value,
                await L2Cache.GetAsync(key));
        }

        [Fact]
        public async Task SetAsync_RemoveAsync()
        {
            var key = "key";
            var value = new byte[] { 0x20, 0x20, 0x20, };

            var memoryKey = $"{RedisCacheOptions.InstanceName}{key}";

            await L1L2Cache.SetAsync(key, value);

            Assert.Equal(
                value,
                await L1L2Cache.GetAsync(key));
            Assert.Equal(
                value,
                L1Cache.Get(memoryKey));
            Assert.Equal(
                value,
                await L2Cache.GetAsync(key));

            await L1L2Cache.RemoveAsync(key);

            Assert.Null(
                await L1L2Cache.GetAsync(key));
            Assert.Null(
                L1Cache.Get(memoryKey));
            Assert.Null(
                await L2Cache.GetAsync(key));
        }
    }
}
