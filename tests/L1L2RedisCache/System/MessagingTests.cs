using System.Text;
using MessagingRedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L1L2RedisCache.Tests.System;

[TestClass]
public class MessagingTests
{
    public MessagingTests()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        EventTimeout = TimeSpan.FromSeconds(5);
    }

    public IConfiguration Configuration { get; }
    public TimeSpan EventTimeout { get; }

    [DataRow(100, MessagingType.Default)]
    [DataRow(100, MessagingType.KeyeventNotifications)]
    [DataRow(100, MessagingType.KeyspaceNotifications)]
    [TestMethod]
    public async Task MessagingTypeTest(
        int iterations,
        MessagingType messagingType)
    {
        var primaryServices = new ServiceCollection();
        primaryServices.AddSingleton(Configuration);
        primaryServices.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
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
            options.MessagingType = messagingType;
        });
        using var secondaryServiceProvider = secondaryServices
            .BuildServiceProvider();

        var secondaryMessageSubscriber = secondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        using var subscribeAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };
        secondaryMessageSubscriber.OnSubscribe += (sender, e) =>
        {
            subscribeAutoResetEvent.Set();
        };

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
                    key, value)
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
                    .GetStringAsync(key)
                    .ConfigureAwait(false));
            Assert.IsTrue(
                Encoding.ASCII.GetBytes(value).SequenceEqual(
                    secondaryL1Cache.Get<byte[]>(key) ?? [ ]));

            // L1 eviction via set
            // L1 population via L2
            await primaryL1L2Cache
                .SetStringAsync(
                    key, value)
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
                    .GetStringAsync(key)
                    .ConfigureAwait(false));
            Assert.IsTrue(
                Encoding.ASCII.GetBytes(value).SequenceEqual(
                    secondaryL1Cache.Get<byte[]>(key) ?? [ ]));

            // L1 eviction via remove
            await primaryL1L2Cache
                .RemoveAsync(key)
                .ConfigureAwait(false);
            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.IsNull(
                secondaryL1Cache
                    .Get(key));
            Assert.IsNull(
                await secondaryL1L2Cache
                    .GetStringAsync(key)
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