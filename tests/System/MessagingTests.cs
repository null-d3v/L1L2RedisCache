using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace L1L2RedisCache.Tests.System;

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
    public async Task MessagingType_Test(
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
        var primaryL1L2Cache = primaryServices
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        var primaryL1L2CacheOptions = primaryServices
            .BuildServiceProvider()
            .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
            .Value;

        var primaryDatabase = (await primaryL1L2CacheOptions
            .ConnectionMultiplexerFactory!.Invoke())
            .GetDatabase(
                primaryL1L2CacheOptions
                    .ConfigurationOptions?
                    .DefaultDatabase ?? -1);
        await primaryDatabase.ExecuteAsync(
            "config",
            "set",
            "notify-keyspace-events",
            NotifyKeyspaceEventsConfig[messagingType]);

        var secondaryServices = new ServiceCollection();
        secondaryServices.AddSingleton(Configuration);
        secondaryServices.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.MessagingType = messagingType;
        });
        var secondaryL1L2Cache = secondaryServices
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();

            await primaryL1L2Cache.SetStringAsync(
                key, value);

            Assert.Equal(
                value,
                await secondaryL1L2Cache
                    .GetStringAsync(key));

            await primaryL1L2Cache.RemoveAsync(key);
            await Task.Delay(25);

            Assert.Null(
                await secondaryL1L2Cache
                    .GetStringAsync(key));
        }
    }
}
