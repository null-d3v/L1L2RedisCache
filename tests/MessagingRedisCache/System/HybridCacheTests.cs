using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public class HybridCacheTests :
    TestsBase
{
    public HybridCacheTests(
        TestContext testContext) :
        base(
            MessagingType.Default,
            testContext)
    {
        PrimaryServices.AddHybridCache();
        SecondaryServices.AddHybridCache();
    }

    public HybridCacheEntryOptions HybridCacheEntryOptions { get; } =
        new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(1),
        };
    public HybridCache PrimaryHybridCache =>
        PrimaryServiceProvider
            .GetRequiredService<HybridCache>();
    public HybridCache SecondaryHybridCache =>
        SecondaryServiceProvider
            .GetRequiredService<HybridCache>();

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }

    [TestMethod]
    public async Task GetOrCreateAsyncTest()
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

            await PrimaryHybridCache
                .SetAsync(
                    key,
                    value,
                    HybridCacheEntryOptions,
                    cancellationToken: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.AreEqual(
                value,
                await PrimaryHybridCache
                    .GetOrCreateAsync(
                        key,
                        cancellationToken =>
                            ValueTask.FromResult<string?>(null),
                        HybridCacheEntryOptions,
                        cancellationToken: TestContext.CancellationToken)
                    .ConfigureAwait(false));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
            Assert.AreEqual(
                value,
                await SecondaryHybridCache
                    .GetOrCreateAsync(
                        key,
                        cancellationToken =>
                            ValueTask.FromResult<string?>(null),
                        HybridCacheEntryOptions,
                        cancellationToken: TestContext.CancellationToken)
                    .ConfigureAwait(false));
            Assert.IsNotNull(
                SecondaryMemoryCache
                    .Get(key));

            await PrimaryHybridCache
                .RemoveAsync(
                    key,
                    cancellationToken: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                PrimaryMemoryCache
                    .Get(key));
            Assert.IsNull(
                SecondaryMemoryCache
                    .Get(key));
        }
    }
}
