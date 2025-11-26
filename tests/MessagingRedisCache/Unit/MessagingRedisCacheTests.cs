using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using StackExchange.Redis;

namespace MessagingRedisCache.Tests.Unit;

[TestClass]
public class MessagingRedisCacheTests
{
    public MessagingRedisCacheTests(
        TestContext testContext)
    {
        TestContext = testContext;

        MessagingRedisCacheOptions = new MessagingRedisCacheOptions
        {
            InstanceName = "L1L2RedisCache:Test:",
        };

        var database = Substitute
            .For<IDatabase>();
        ConnectionMultiplexer = Substitute
            .For<IConnectionMultiplexer>();
        ConnectionMultiplexer
            .GetDatabase(
                Arg.Any<int>(),
                Arg.Any<object>())
            .Returns(database);

        MessagingRedisCacheOptions.ConnectionMultiplexerFactory =
            () => Task.FromResult(ConnectionMultiplexer);

        MessagePublisher = Substitute
            .For<IMessagePublisher>();
        MessagePublisher
            .Publish(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<RedisKey>());
        MessagePublisher
            .PublishAsync(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<RedisKey>(),
                Arg.Any<CancellationToken>());

        MessageSubscriber = Substitute
            .For<IMessageSubscriber>();
        MessageSubscriber
            .SubscribeAsync(
                Arg.Any<IConnectionMultiplexer>(),
                Arg.Any<CancellationToken>());

        var messagingConfigurationVerifier = Substitute
            .For<IMessagingConfigurationVerifier>();
        messagingConfigurationVerifier
            .VerifyConfigurationAsync(
                Arg.Any<IDatabase>(),
                Arg.Any<CancellationToken>());

        var bufferDistributedCache = Substitute
            .For<IBufferDistributedCache>();
        MessagingRedisCache = new MessagingRedisCache(
            bufferDistributedCache,
            MessagePublisher,
            MessageSubscriber,
            messagingConfigurationVerifier,
            Options.Create(MessagingRedisCacheOptions));
    }

    public IConnectionMultiplexer ConnectionMultiplexer { get; }
    public IMessagePublisher MessagePublisher { get; }
    public IMessageSubscriber MessageSubscriber { get; }
    public MessagingRedisCacheOptions MessagingRedisCacheOptions { get; }
    public MessagingRedisCache MessagingRedisCache { get; }
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RemoveAsyncTest()
    {
        var key = "key";

        await MessagingRedisCache
            .RemoveAsync(
                key,
                token: TestContext.CancellationToken)
            .ConfigureAwait(false);

        await MessagePublisher
            .Received()
            .PublishAsync(
                Arg.Any<IConnectionMultiplexer>(),
                key,
                cancellationToken: TestContext.CancellationToken)
            .ConfigureAwait(false);
    }

    [TestMethod]
    public void RemoveTest()
    {
        var key = "key";

        MessagingRedisCache
            .Remove(key);

        MessagePublisher
            .Received()
            .Publish(
                Arg.Any<IConnectionMultiplexer>(),
                key);
    }

    [TestMethod]
    public async Task SetAsyncTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        await MessagingRedisCache
            .SetAsync(
                key,
                value,
                token: TestContext.CancellationToken)
            .ConfigureAwait(false);

        await MessagePublisher
            .Received()
            .PublishAsync(
                Arg.Any<IConnectionMultiplexer>(),
                key,
                cancellationToken: TestContext.CancellationToken)
            .ConfigureAwait(false);
    }

    [TestMethod]
    public void SetTest()
    {
        var key = "key";
        var value = "   "u8.ToArray();

        MessagingRedisCache
            .Set(key, value);

        MessagePublisher
            .Received()
            .Publish(
                Arg.Any<IConnectionMultiplexer>(),
                key);
    }
}
