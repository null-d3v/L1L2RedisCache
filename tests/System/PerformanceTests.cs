using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit;

namespace L1L2RedisCache.Tests.System;

public class PerformanceTests
{
    public PerformanceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddL1L2RedisCache(options =>
        {
            configuration.Bind("L1L2RedisCache", options);
        });
        var serviceProvider = services
            .BuildServiceProvider();

        L1Cache = serviceProvider
            .GetRequiredService<IMemoryCache>();
        L1L2Cache = serviceProvider
            .GetRequiredService<IDistributedCache>();
        L2Cache = serviceProvider
            .GetRequiredService<Func<IDistributedCache>>()
            .Invoke();
        Stopwatch = new Stopwatch();
    }

    public IMemoryCache L1Cache { get; }
    public IDistributedCache L1L2Cache { get; }
    public IDistributedCache L2Cache { get; }
    public Stopwatch Stopwatch { get; }

    [InlineData(10000)]
    [Theory]
    public async Task Performance_Test(
        int iterations)
    {
        Stopwatch.Restart();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            await L2Cache.SetStringAsync(
                $"Performance:{iteration}",
                "Value",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromMinutes(5),
                });
        }
        Stopwatch.Stop();
        var l2SetTicks = Stopwatch.ElapsedTicks;

        Stopwatch.Restart();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            await L1L2Cache.GetStringAsync(
                $"Performance:{iteration}");
        }
        Stopwatch.Stop();
        var l1L2GetPropagationTicks = Stopwatch.ElapsedTicks;

        Stopwatch.Restart();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            await L1L2Cache.SetStringAsync(
                $"Performance:{iteration}",
                "Value",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromMinutes(5),
                });
        }
        Stopwatch.Stop();
        var l1L2SetTicks = Stopwatch.ElapsedTicks;

        Stopwatch.Restart();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            await L2Cache.GetStringAsync(
                $"Performance:{iteration}");
        }
        Stopwatch.Stop();
        var l2GetTicks = Stopwatch.ElapsedTicks;

        Stopwatch.Restart();
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            await L1L2Cache.GetStringAsync(
                $"Performance:{iteration}");
        }
        Stopwatch.Stop();
        var l1L2GetTicks = Stopwatch.ElapsedTicks;

        Assert.True(
            l1L2SetTicks / l2SetTicks < 3,
            "L1L2Cache Set cannot perform significantly worse than RedisCache Set.");

        Assert.True(
            l2GetTicks / l1L2GetTicks > 100,
            "L1L2Cache Get must perform significantly better than RedisCache Get.");

        Assert.True(
            l1L2GetPropagationTicks / l2GetTicks < 3,
            "L1L2Cache Get with propagation cannot perform significantly worse than RedisCache Get.");
    }
}
