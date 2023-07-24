using Microsoft.Extensions.Caching.Distributed;
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
            .AddEnvironmentVariables()
            .Build();
        NotifyKeyspaceEventsConfig = new Dictionary<MessagingType, string>
        {
            { MessagingType.Default, string.Empty },
            { MessagingType.KeyeventNotifications, "ghE" },
            { MessagingType.KeyspaceNotifications, "ghK" },
        };
    }

    public IConfiguration Configuration { get; }
    public IDictionary<MessagingType, string> NotifyKeyspaceEventsConfig { get; }

    [DataRow(0, MessagingType.Default)]
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
        var primaryServiceProvider = primaryServices
            .BuildServiceProvider();

        var primaryL1L2Cache = primaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        var primaryL1L2CacheOptions = primaryServiceProvider
            .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
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
        var secondaryServiceProvider = secondaryServices
            .BuildServiceProvider();

        var secondaryMessageSubscriber = secondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var messageAutoResetEvent = new AutoResetEvent(false);
        secondaryMessageSubscriber.OnMessage += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };
        secondaryMessageSubscriber.OnSubscribe += (sender, e) =>
        {
            messageAutoResetEvent.Set();
        };

        var secondaryL1L2Cache = secondaryServiceProvider
            .GetRequiredService<IDistributedCache>();

        Assert.IsTrue(
            messageAutoResetEvent
                .WaitOne(TimeSpan.FromSeconds(5)));

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await primaryL1L2Cache
                .SetStringAsync(
                    key, value)
                .ConfigureAwait(false);

            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(TimeSpan.FromSeconds(5)));

            Assert.AreEqual(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false));

            await primaryL1L2Cache
                .RemoveAsync(key)
                .ConfigureAwait(false);

            Assert.IsTrue(
                messageAutoResetEvent
                    .WaitOne(TimeSpan.FromSeconds(5)));

            Assert.IsNull(
                await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false));
        }

        await primaryServiceProvider
            .DisposeAsync()
            .ConfigureAwait(false);
        await secondaryServiceProvider
            .DisposeAsync()
            .ConfigureAwait(false);
    }

    private async Task SetAndVerifyConfigurationAsync(
        IServiceProvider serviceProvider,
        MessagingType messagingType)
    {
        var l1L2CacheOptions = serviceProvider
            .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
            .Value;

        var database = (await l1L2CacheOptions
            .ConnectionMultiplexerFactory!
            .Invoke()
            .ConfigureAwait(false))
            .GetDatabase(
                l1L2CacheOptions
                    .ConfigurationOptions?
                    .DefaultDatabase ?? -1);
        await database
            .ExecuteAsync(
                "config",
                "set",
                "notify-keyspace-events",
                NotifyKeyspaceEventsConfig[messagingType])
            .ConfigureAwait(false);

        var configurationVerifier = serviceProvider
            .GetRequiredService<IConfigurationVerifier>();
        Assert.IsTrue(
            await configurationVerifier
                .VerifyConfigurationAsync(
                    "notify-keyspace-events",
                    CancellationToken.None,
                    NotifyKeyspaceEventsConfig[messagingType])
                .ConfigureAwait(false));
    }
}
