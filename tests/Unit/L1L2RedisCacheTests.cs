using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using StackExchange.Redis;

namespace L1L2RedisCache.Tests.Unit;

[TestClass]
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

        var database = Substitute
            .For<IDatabase>();
        database
            .HashGetAll(
                Arg.Any<RedisKey>(),
                Arg.Any<CommandFlags>())
            .Returns(
                args =>
                {
                    var key = ((RedisKey)args[0]).ToString()[
                        (L1L2RedisCacheOptions?.InstanceName?.Length ?? 0)..];
                    var value = L2Cache.Get(key);
                    return
                    [
                        new HashEntry("data", value),
                    ];
                });
        database
            .HashGetAllAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<CommandFlags>())
            .Returns(
                async args =>
                {
                    var key = ((RedisKey)args[0]).ToString()[
                        (L1L2RedisCacheOptions?.InstanceName?.Length ?? 0)..];
                    var value = await L2Cache
                        .GetAsync(key)
                        .ConfigureAwait(false);
                    return
                    [
                        new HashEntry("data", value),
                    ];
                });
        database
            .KeyExists(
                Arg.Any<RedisKey>(),
                Arg.Any<CommandFlags>())
            .Returns(
                args =>
                {
                    return L2Cache.Get(
                        ((RedisKey)args[0]).ToString()) != null;
                });
        database
            .KeyExistsAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<CommandFlags>())
            .Returns(
                async args =>
                {
                    var key = ((RedisKey)args[0]).ToString()[
                        (L1L2RedisCacheOptions.InstanceName?.Length ?? 0)..];
                    return await L2Cache
                        .GetAsync(key)
                        .ConfigureAwait(false) != null;
                });

        var connectionMultiplexer = Substitute
            .For<IConnectionMultiplexer>();
        connectionMultiplexer
            .GetDatabase(
                Arg.Any<int>(),
                Arg.Any<object>())
            .Returns(database);

        L1L2RedisCacheOptions.ConnectionMultiplexerFactory =
            () => Task.FromResult(connectionMultiplexer);

        var messagePublisher = Substitute
            .For<IMessagePublisher>();
        messagePublisher
            .Publish(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<string>());
        messagePublisher
            .PublishAsync(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        var messageSubscriber = Substitute
            .For<IMessageSubscriber>();
        messageSubscriber
            .SubscribeAsync(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<CancellationToken>());

        var messagingConfigurationVerifier = Substitute
            .For<IMessagingConfigurationVerifier>();
        messagingConfigurationVerifier
            .VerifyConfigurationAsync(
                Arg.Any<IDatabase>(),
                Arg.Any<CancellationToken>());

        L1L2Cache = new L1L2RedisCache(
            L1Cache,
            L1L2RedisCacheOptions,
            new Func<IDistributedCache>(() => L2Cache),
            messagePublisher,
            messageSubscriber,
            messagingConfigurationVerifier);
    }

    public IMemoryCache L1Cache { get; }
    public IDistributedCache L1L2Cache { get; }
    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; }
    public IDistributedCache L2Cache { get; }

    [TestMethod]
    public async Task GetPropagationTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.IsNull(
            L1Cache.Get(prefixedKey));
        Assert.AreEqual(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.AreEqual(
            value,
            L1Cache.Get(prefixedKey));
    }

    [TestMethod]
    public void SetTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        L1L2Cache.Set(key, value);

        Assert.AreEqual(
            value,
            L1L2Cache.Get(key));
        Assert.AreEqual(
            value,
            L1Cache.Get(prefixedKey));
        Assert.AreEqual(
            value,
            L2Cache.Get(key));
    }

    [TestMethod]
    public void SetRemoveTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        L1L2Cache.Set(key, value);

        Assert.AreEqual(
            value,
            L1L2Cache.Get(key));
        Assert.AreEqual(
            value,
            L1Cache.Get(prefixedKey));
        Assert.AreEqual(
            value,
            L2Cache.Get(key));

        L1L2Cache.Remove(key);

        Assert.IsNull(
            L1L2Cache.Get(key));
        Assert.IsNull(
            L1Cache.Get(prefixedKey));
        Assert.IsNull(
            L2Cache.Get(key));
    }

    [TestMethod]
    public async Task SetAsyncTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L1L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.AreEqual(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.AreEqual(
            value,
            L1Cache.Get(prefixedKey));
        Assert.AreEqual(
            value,
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
    }

    [TestMethod]
    public async Task SetAsyncRemoveAsyncTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        var prefixedKey = $"{L1L2RedisCacheOptions.KeyPrefix}{key}";

        await L1L2Cache
            .SetAsync(key, value)
            .ConfigureAwait(false);

        Assert.AreEqual(
            value,
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.AreEqual(
            value,
            L1Cache.Get(prefixedKey));
        Assert.AreEqual(
            value,
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));

        await L1L2Cache
            .RemoveAsync(key)
            .ConfigureAwait(false);

        Assert.IsNull(
            await L1L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
        Assert.IsNull(
            L1Cache.Get(prefixedKey));
        Assert.IsNull(
            await L2Cache
                .GetAsync(key)
                .ConfigureAwait(false));
    }
}
