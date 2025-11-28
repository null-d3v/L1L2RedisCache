using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace L1L2RedisCache.Tests.System;

[TestClass]
public class ReliabilityTests(
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

    [TestMethod]
    public void InitializeBadConnectionTest()
    {
        using var subscribeAutoResetEvent =
            new AutoResetEvent(false);

        var services = new ServiceCollection();
        services.AddSingleton(Configuration);
        services.AddL1L2RedisCache(options =>
        {
            Configuration.Bind("L1L2RedisCache", options);
            options.Events.OnSubscribe = () =>
            {
                subscribeAutoResetEvent.Set();
                return Task.CompletedTask;
            };
            options.Configuration = "localhost:80";
        });
        using var serviceProvider = services
            .BuildServiceProvider();

        var l1L2Cache = serviceProvider
            .GetRequiredService<IDistributedCache>();

        Assert.IsFalse(
            subscribeAutoResetEvent
                .WaitOne(EventTimeout));
        Assert.ThrowsAsync<RedisConnectionException>(
            () => l1L2Cache
                .GetStringAsync(
                    string.Empty,
                    token: TestContext.CancellationToken));
    }
}