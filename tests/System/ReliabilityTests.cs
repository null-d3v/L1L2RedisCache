using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace L1L2RedisCache.Tests.System;

[Collection("System")]
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

    [Fact]
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

        Assert.False(
            subscribeAutoResetEvent
                .WaitOne(EventTimeout));
        Assert.ThrowsAsync<RedisConnectionException>(
            () => l1L2Cache
                .GetStringAsync(string.Empty));
    }
}
