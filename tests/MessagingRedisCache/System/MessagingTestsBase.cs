using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public abstract class MessagingTestsBase :
    TestsBase
{
    protected MessagingTestsBase(
        MessagingType messagingType,
        TestContext testContext) :
        base(
            messagingType,
            testContext)
    {
        SecondaryOptionsConfigureAction = options =>
        {
            options.Events.OnMessageRecieved = channelMessage =>
            {
                MessageAutoResetEvent.Set();
                return Task.CompletedTask;
            };
        };
    }

    public AutoResetEvent MessageAutoResetEvent { get; } =
        new AutoResetEvent(false);

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }

    [TestMethod]
    public async Task RemoveAsyncTest()
    {
        await SetAndVerifyConfigurationAsync()
            .ConfigureAwait(false);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await PrimaryDistributedCache
                .SetStringAsync(
                    key,
                    value,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));

            SecondaryMemoryCache
                .Set(key, value);
            await PrimaryDistributedCache
                .RemoveAsync(
                    key,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }

    [TestMethod]
    [SuppressMessage("Performance", "CA1849")]
    public async Task RemoveTest()
    {
        await SetAndVerifyConfigurationAsync()
            .ConfigureAwait(false);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await PrimaryDistributedCache
                .SetStringAsync(
                    key,
                    value,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));

            SecondaryMemoryCache
                .Set(key, value);
            PrimaryDistributedCache
                .Remove(
                    key);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }

    [TestMethod]
    public async Task SetAsyncTest()
    {
        await SetAndVerifyConfigurationAsync()
            .ConfigureAwait(false);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            SecondaryMemoryCache
                .Set(key, value);
            await PrimaryDistributedCache
                .SetStringAsync(
                    key,
                    value,
                    DistributedCacheEntryOptions,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }

    [TestMethod]
    [SuppressMessage("Performance", "CA1849")]
    public async Task SetTest()
    {
        await SetAndVerifyConfigurationAsync()
            .ConfigureAwait(false);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            SecondaryMemoryCache
                .Set(key, value);
            PrimaryDistributedCache
                .Set(
                    key,
                    Encoding.ASCII.GetBytes(value),
                    DistributedCacheEntryOptions);
            Assert.IsTrue(
                MessageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }
}
