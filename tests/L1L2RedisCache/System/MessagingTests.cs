using MessagingRedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace L1L2RedisCache.Tests.System;

[TestClass]
public class MessagingTests(
    TestContext testContext)
{
    public IConfiguration Configuration { get; } =
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
    public TimeSpan EventTimeout { get; } =
        TimeSpan.FromSeconds(5);
    public TestContext TestContext { get; set; } =
        testContext;

    [DataRow(100, MessagingType.Default)]
    [DataRow(100, MessagingType.KeyeventNotifications)]
    [DataRow(100, MessagingType.KeyspaceNotifications)]
    [TestMethod]
    public async Task MessagingTypeTest(
        int iterations,
        MessagingType messagingType)
    {
        using var messageAutoResetEvent =
            new AutoResetEvent(false);
        using var subscribeAutoResetEvent =
            new AutoResetEvent(false);

        var primaryServices = new ServiceCollection();
        primaryServices.AddSingleton(Configuration);
        primaryServices.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.Events.OnSubscribe = () =>
            {
                subscribeAutoResetEvent.Set();
                return Task.CompletedTask;
            };
            options.MessagingType = messagingType;
        });
        using var primaryServiceProvider = primaryServices
            .BuildServiceProvider();

        var primaryL1L2Cache = primaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        var primaryMessagingRedisCacheOptions = primaryServiceProvider
            .GetRequiredService<IOptions<MessagingRedisCacheOptions>>()
            .Value;

        await SetAndVerifyConfigurationAsync(
            primaryServiceProvider,
            messagingType)
            .ConfigureAwait(false);

        var secondaryServices = new ServiceCollection();
        secondaryServices.AddSingleton(Configuration);
        secondaryServices.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.Events.OnMessageRecieved = channelMessage =>
            {
                messageAutoResetEvent.Set();
                return Task.CompletedTask;
            };
            options.MessagingType = messagingType;
        });
        using var secondaryServiceProvider = secondaryServices
            .BuildServiceProvider();

        var secondaryL1Cache = secondaryServiceProvider
            .GetRequiredService<IMemoryCache>();
        var secondaryL1L2Cache = secondaryServiceProvider
            .GetRequiredService<IDistributedCache>();

        Assert.IsTrue(
            subscribeAutoResetEvent
                .WaitOne(EventTimeout));

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            // L1 population via L2
            await primaryL1L2Cache
                .SetStringAsync(
                    key,
                    value,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                secondaryL1Cache
                    .Get(key));
            Assert.AreEqual(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(
                        key,
                        token: TestContext.CancellationToken)
                    .ConfigureAwait(false));
            Assert.IsTrue(
                Encoding.ASCII.GetBytes(value).SequenceEqual(
                    secondaryL1Cache.Get<byte[]>(key) ?? [ ]));

            // L1 eviction via set
            // L1 population via L2
            await primaryL1L2Cache
                .SetStringAsync(
                    key,
                    value,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                secondaryL1Cache
                    .Get(key));
            Assert.AreEqual(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(
                        key,
                        token: TestContext.CancellationToken)
                    .ConfigureAwait(false));
            Assert.IsTrue(
                Encoding.ASCII.GetBytes(value).SequenceEqual(
                    secondaryL1Cache.Get<byte[]>(key) ?? [ ]));

            // L1 eviction via remove
            await primaryL1L2Cache
                .RemoveAsync(
                    key,
                    token: TestContext.CancellationToken)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                secondaryL1Cache
                    .Get(key));
            Assert.IsNull(
                await secondaryL1L2Cache
                    .GetStringAsync(
                        key,
                        token: TestContext.CancellationToken)
                    .ConfigureAwait(false));
        }
    }

    private static async Task SetAndVerifyConfigurationAsync(
        IServiceProvider serviceProvider,
        MessagingType messagingType)
    {
        var l1L2Cache = serviceProvider
            .GetRequiredService<IDistributedCache>() as L1L2RedisCache;

        await l1L2Cache!.Database.Value
            .ExecuteAsync(
                "config",
                "set",
                "notify-keyspace-events",
                MessagingConfigurationVerifier
                    .NotifyKeyspaceEventsConfig[messagingType])
            .ConfigureAwait(false);

        var configurationVerifier = serviceProvider
            .GetRequiredService<IMessagingConfigurationVerifier>();
        Assert.IsTrue(
            await configurationVerifier
                .VerifyConfigurationAsync(
                    l1L2Cache.Database.Value)
                .ConfigureAwait(false));
    }
}