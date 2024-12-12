using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public abstract class TestsBase
{
    internal TestsBase(
        MessagingType messagingType)
    {
        MessagingType = messagingType;

        PrimaryServices = new ServiceCollection();
        PrimaryServices.AddSingleton(Configuration);
        PrimaryServices.AddMemoryCache();
        PrimaryServices
            .AddMessagingRedisCache(options =>
            {
                Configuration.Bind("MessagingRedisCache", options);
                options.MessagingType = MessagingType;
            })
            .AddMemoryCacheSubscriber();;

        SecondaryServices = new ServiceCollection();
        SecondaryServices.AddSingleton(Configuration);
        SecondaryServices.AddMemoryCache();
        SecondaryServices
            .AddMessagingRedisCache(options =>
            {
                Configuration.Bind("MessagingRedisCache", options);
                options.MessagingType = MessagingType;
            })
            .AddMemoryCacheSubscriber();
    }

    public IConfiguration Configuration { get; } =
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
    public DistributedCacheEntryOptions DistributedCacheEntryOptions { get; } =
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromHours(1),
        };
    public TimeSpan EventTimeout { get; } =
        TimeSpan.FromSeconds(10);
    public int Iterations { get; } = 100;
    public MessagingType MessagingType { get; }
    public IDistributedCache PrimaryDistributedCache { get; private set; } = default!;
    public IMemoryCache PrimaryMemoryCache { get; private set; } = default!;
    public IServiceProvider PrimaryServiceProvider { get; private set; } = default!;
    public IDistributedCache SecondaryDistributedCache { get; private set; } = default!;
    public IMemoryCache SecondaryMemoryCache { get; private set; } = default!;
    public IServiceProvider SecondaryServiceProvider { get; private set; } = default!;

    protected IServiceCollection PrimaryServices { get; private set; } = default!;
    protected IServiceCollection SecondaryServices { get; private set; } = default!;

    [TestInitialize]
    public virtual void TestInitialize()
    {
        PrimaryServiceProvider = PrimaryServices
            .BuildServiceProvider();
        PrimaryDistributedCache = PrimaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        PrimaryMemoryCache = PrimaryServiceProvider
            .GetRequiredService<IMemoryCache>();

        SecondaryServiceProvider = SecondaryServices
            .BuildServiceProvider();
        SecondaryDistributedCache = SecondaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        SecondaryMemoryCache = SecondaryServiceProvider
            .GetRequiredService<IMemoryCache>();
    }

    public async Task SetAndVerifyConfigurationAsync()
    {
        var messagingRedisCache = PrimaryServiceProvider
            .GetRequiredService<IDistributedCache>() as MessagingRedisCache;

        await messagingRedisCache!.Database.Value
            .ExecuteAsync(
                "config",
                "set",
                "notify-keyspace-events",
                MessagingConfigurationVerifier
                    .NotifyKeyspaceEventsConfig[MessagingType])
            .ConfigureAwait(false);

        var configurationVerifier = PrimaryServiceProvider
            .GetRequiredService<IMessagingConfigurationVerifier>();
        Assert.IsTrue(
            await configurationVerifier
                .VerifyConfigurationAsync(
                    messagingRedisCache.Database.Value)
                .ConfigureAwait(false));
    }
}
