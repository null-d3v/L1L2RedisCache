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
        NotifyKeyspaceEventsConfig = new Dictionary<MessagingType, string>
        {
            { MessagingType.Default, string.Empty },
            { MessagingType.KeyeventNotifications, "ghE" },
            { MessagingType.KeyspaceNotifications, "ghK" },
        };
    }

    public IConfiguration Configuration { get; }
    public IDictionary<MessagingType, string> NotifyKeyspaceEventsConfig { get; }

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
        var primaryServiceProvider = primaryServices
            .BuildServiceProvider();

        var primaryL1L2Cache = primaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        var primaryL1L2CacheOptions = primaryServiceProvider
            .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
            .Value;

        var primaryMessageSubscriber = primaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        await primaryMessageSubscriber
            .SubscribeAsync()
            .ConfigureAwait(false);

        var primaryDatabase = (await primaryL1L2CacheOptions
            .ConnectionMultiplexerFactory!
            .Invoke()
            .ConfigureAwait(false))
            .GetDatabase(
                primaryL1L2CacheOptions
                    .ConfigurationOptions?
                    .DefaultDatabase ?? -1);
        await primaryDatabase
            .ExecuteAsync(
                "config",
                "set",
                "notify-keyspace-events",
                NotifyKeyspaceEventsConfig[messagingType])
            .ConfigureAwait(false);

        var configurationVerifier = primaryServiceProvider
            .GetRequiredService<IConfigurationVerifier>();
        Assert.True(
            await configurationVerifier
                .VerifyConfigurationAsync(
                    "notify-keyspace-events",
                    CancellationToken.None,
                    NotifyKeyspaceEventsConfig[messagingType])
                .ConfigureAwait(false));

        var secondaryServices = new ServiceCollection();
        secondaryServices.AddSingleton(Configuration);
        secondaryServices.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.MessagingType = messagingType;
        });
        var secondaryServiceProvider = secondaryServices
            .BuildServiceProvider();

        var secondaryL1L2Cache = secondaryServiceProvider
            .GetRequiredService<IDistributedCache>();

        var secondaryMessageSubscriber = secondaryServiceProvider
            .GetRequiredService<IMessageSubscriber>();
        await secondaryMessageSubscriber
            .SubscribeAsync()
            .ConfigureAwait(false);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await primaryL1L2Cache
                .SetStringAsync(
                    key, value)
                .ConfigureAwait(false);

            Assert.Equal(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false));

            await primaryL1L2Cache
                .RemoveAsync(key)
                .ConfigureAwait(false);

            var secondaryValue = await secondaryL1L2Cache
                .GetStringAsync(key)
                .ConfigureAwait(false);
            var attempts = 1;
            while (attempts < 25 && secondaryValue != null)
            {
                attempts++;
                await Task
                    .Delay(25)
                    .ConfigureAwait(false);
                secondaryValue = await secondaryL1L2Cache
                    .GetStringAsync(key)
                    .ConfigureAwait(false);
            }

            Assert.Null(secondaryValue);
        }

        await primaryMessageSubscriber
            .UnsubscribeAsync()
            .ConfigureAwait(false);
        await primaryServiceProvider
            .DisposeAsync()
            .ConfigureAwait(false);
        await secondaryMessageSubscriber
            .UnsubscribeAsync()
            .ConfigureAwait(false);
        await secondaryServiceProvider
            .DisposeAsync()
            .ConfigureAwait(false);
    }
}
