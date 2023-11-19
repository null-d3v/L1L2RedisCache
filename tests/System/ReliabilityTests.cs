using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;

namespace L1L2RedisCache.Tests.System;

[TestClass]
public class ReliabilityTests
{
    public ReliabilityTests()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
        EventTimeout = TimeSpan.FromSeconds(5);
    }

    public IConfiguration Configuration { get; }
    public TimeSpan EventTimeout { get; }

    [TestMethod]
    public void InitializeBadConnectionTest()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Configuration);
        services.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.Configuration = "localhost:80";
        });
        using var serviceProvider = services
            .BuildServiceProvider();

        var messageSubscriber = serviceProvider
            .GetRequiredService<IMessageSubscriber>();
        using var subscribeAutoResetEvent = new AutoResetEvent(false);
        messageSubscriber.OnSubscribe += (sender, e) =>
        {
            subscribeAutoResetEvent.Set();
        };

        var l1L2Cache = serviceProvider
            .GetRequiredService<IDistributedCache>();

        Assert.IsFalse(
            subscribeAutoResetEvent
                .WaitOne(EventTimeout));
        Assert.ThrowsExceptionAsync<RedisConnectionException>(
            () => l1L2Cache
                .GetStringAsync(string.Empty));
    }
}
