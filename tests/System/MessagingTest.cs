using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace L1L2RedisCache.Test.System;

public class MessagingTest
{
    public MessagingTest()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
    }

    public IConfiguration Configuration { get; }

    [InlineData(MessagingType.Default)]
    [InlineData(MessagingType.KeyeventNotifications)]
    [InlineData(MessagingType.KeyspaceNotifications)]
    [Theory]
    public async Task MessagingType_Test(
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

        var key = Guid.NewGuid().ToString();
        var value = "value";

        await primaryL1L2Cache.SetStringAsync(
            key, value);

        Assert.Equal(
            value,
            await secondaryL1L2Cache
                .GetStringAsync(key));

        await primaryL1L2Cache.RemoveAsync(key);
        await Task.Delay(1000);

        Assert.Null(
            await secondaryL1L2Cache
                .GetStringAsync(key));
    }
}
