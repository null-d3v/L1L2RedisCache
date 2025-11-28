using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

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

        SecondaryOptionsConfigureAction = options =>
        {
            options.Events.OnMessageRecieved = channelMessage =>
            {
                MessageAutoResetEvent.Set();
                return Task.CompletedTask;
            };
        };
    }

    public HybridCacheEntryOptions HybridCacheEntryOptions { get; } =
        new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(1),
        };
    public AutoResetEvent MessageAutoResetEvent { get; } =
        new AutoResetEvent(false);
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
                MessageAutoResetEvent
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
                MessageAutoResetEvent
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
