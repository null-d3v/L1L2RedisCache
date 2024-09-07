using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace L1L2RedisCache.Tests.System;

public abstract class BenchmarkBase
{
    [Params(100)]
    public int Iterations { get; set; }
    
    protected DistributedCacheEntryOptions DistributedCacheEntryOptions { get; set; } =
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromHours(1),
        };
    protected IMemoryCache? L1Cache { get; set; }
    protected IDistributedCache? L1L2Cache { get; set; }
    protected IDistributedCache? L2Cache { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
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
    }
}