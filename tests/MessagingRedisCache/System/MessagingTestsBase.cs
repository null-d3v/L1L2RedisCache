using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public abstract class MessagingTestsBase(
    MessagingType messagingType) :
    TestsBase(
        messagingType)
{
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

        var secondaryMessageSubscriber = SecondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await PrimaryDistributedCache
                .SetStringAsync(key, value)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));

            SecondaryMemoryCache
                .Set(key, value);
            await PrimaryDistributedCache
                .RemoveAsync(
                    key)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
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

        var secondaryMessageSubscriber = SecondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await PrimaryDistributedCache
                .SetStringAsync(key, value)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));

            SecondaryMemoryCache
                .Set(key, value);
            PrimaryDistributedCache
                .Remove(
                    key);
            Assert.IsTrue(
                messageAutoResetEvent
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

        var secondaryMessageSubscriber = SecondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };

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
                    DistributedCacheEntryOptions)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
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

        var secondaryMessageSubscriber = SecondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };

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
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }
}
