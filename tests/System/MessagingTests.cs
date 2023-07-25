using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace L1L2RedisCache.Tests.System;

[Collection("System")]
public class MessagingTests
{
    public MessagingTests()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
        EventTimeout = TimeSpan.FromSeconds(5);
    }

    public IConfiguration Configuration { get; }
    public TimeSpan EventTimeout { get; }

    [InlineData(100, MessagingType.Default)]
    [InlineData(100, MessagingType.KeyeventNotifications)]
    [InlineData(100, MessagingType.KeyspaceNotifications)]
    [Theory]
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

        var secondaryL1L2Cache = secondaryServiceProvider
            .GetRequiredService<IDistributedCache>();

        Assert.True(
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
            Assert.True(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.Equal(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false));

            // L1 eviction via set
            // L1 population via L2
            await primaryL1L2Cache
                .SetStringAsync(
                    key, value)
                .ConfigureAwait(false);
            Assert.True(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.Equal(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false));

            // L1 eviction via remove
            await primaryL1L2Cache
                .RemoveAsync(key)
                .ConfigureAwait(false);
            Assert.True(
                messageAutoResetEvent
                    .WaitOne(EventTimeout));
            Assert.Null(
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
        Assert.True(
            await configurationVerifier
                .VerifyConfigurationAsync(
                    l1L2Cache.Database.Value)
                .ConfigureAwait(false));
    }
}
