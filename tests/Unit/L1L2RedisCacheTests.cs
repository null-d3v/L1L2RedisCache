using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace L1L2RedisCache.Tests.Unit;

[Collection("Unit")]
public class L1L2RedisCacheTests
{
    public L1L2RedisCacheTests()
    {
        L1Cache = new MemoryCache(
            Options.Create(new MemoryCacheOptions()));

        L2Cache = new MemoryDistributedCache(
            Options.Create(
                new MemoryDistributedCacheOptions()));

        L1L2RedisCacheOptions = Options
            .Create(
                new L1L2RedisCacheOptions
                {
                    InstanceName = "L1L2RedisCache:Test:",
                })
            .Value;

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(
                d => d.HashGetAll(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>(
                (k, cF) =>
                {
                    var key = (k.ToString())[
                        (L1L2RedisCacheOptions?.InstanceName?.Length ?? 0)..];
                    var value = L2Cache.Get(key);
                    return new HashEntry[]
                    {
                        new HashEntry("data", value),
                    };
                });
        mockDatabase
            .Setup(
                d => d.HashGetAllAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>(
                (k, cF) =>
                {
                    var key = k.ToString()[
                        (L1L2RedisCacheOptions?.InstanceName?.Length ?? 0)..];
                    var value = L2Cache.Get(key);
                    return Task.FromResult(new HashEntry[]
                    {
                        new HashEntry("data", value),
                    });
                });
        mockDatabase
            .Setup(
                d => d.KeyExists(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>(
                (k, cF) => L2Cache.Get(k.ToString()) != null);
        mockDatabase
            .Setup(
                d => d.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>(
                async (k, cF) =>
                {
                    var key = k.ToString()[
                        (L1L2RedisCacheOptions.InstanceName?.Length ?? 0)..];
                    return await L2Cache
                        .GetAsync(key)
                        .ConfigureAwait(false) != null;
                });

        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer
            .Setup(cM => cM.GetDatabase(
                It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        L1L2RedisCacheOptions.ConnectionMultiplexerFactory =
            () => Task.FromResult(mockConnectionMultiplexer.Object);

        var mockMessagePublisher = new Mock<IMessagePublisher>();
        mockMessagePublisher
            .Setup(mP => mP.Publish(
                It.IsAny<string>()));
        mockMessagePublisher
            .Setup(mP => mP.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()));

        var mockMessageSubscriber = new Mock<IMessageSubscriber>();
        mockMessageSubscriber
            .Setup(mS => mS.SubscribeAsync(
                It.IsAny<CancellationToken>()));

        var jsonSerializerOptions = Options.Create(
            new JsonSerializerOptions());

        L1L2Cache = new L1L2RedisCache(
            L1Cache,
            L1L2RedisCacheOptions,
            new Func<IDistributedCache>(() => L2Cache),
            mockMessagePublisher.Object,
            mockMessageSubscriber.Object);
    }

    public IMemoryCache L1Cache { get; }
    public IDistributedCache L1L2Cache { get; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; }
    public IDistributedCache L2Cache { get; }

    [Fact]
    public async Task GetPropagationTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.Null(
            L1Cache.Get(prefixedKey));
        Assert.Equal(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.Equal(
            value,
            L1Cache.Get(prefixedKey));
    }

    [Fact]
    public void SetTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        L1L2Cache.Set(key, value);

        Assert.Equal(
            value,
            L1L2Cache.Get(key));
        Assert.Equal(
            value,
            L1Cache.Get(prefixedKey));
        Assert.Equal(
            value,
            L2Cache.Get(key));
    }

    [Fact]
    public void SetRemoveTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        L1L2Cache.Set(key, value);

        Assert.Equal(
            value,
            L1L2Cache.Get(key));
        Assert.Equal(
            value,
            L1Cache.Get(prefixedKey));
        Assert.Equal(
            value,
            L2Cache.Get(key));

        L1L2Cache.Remove(key);

        Assert.Null(
            L1L2Cache.Get(key));
        Assert.Null(
            L1Cache.Get(prefixedKey));
        Assert.Null(
            L2Cache.Get(key));
    }

    [Fact]
    public async Task SetAsyncTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L1L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.Equal(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.Equal(
            value,
            L1Cache.Get(prefixedKey));
        Assert.Equal(
            value,
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
    }

    [Fact]
    public async Task SetAsyncRemoveAsyncTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L1L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.Equal(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.Equal(
            value,
            L1Cache.Get(prefixedKey));
        Assert.Equal(
            value,
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));

        await L1L2Cache
            .RemoveAsync(key)
            .ConfigureAwait(false);

        Assert.Null(
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.Null(
            L1Cache.Get(prefixedKey));
        Assert.Null(
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
    }
}
