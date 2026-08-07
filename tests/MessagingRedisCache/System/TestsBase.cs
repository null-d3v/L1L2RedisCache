using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessagingRedisCache.Tests.System;

public abstract class TestsBase
{
    internal TestsBase(
        MessagingType messagingType,
        TestContext testContext)
    {
        MessagingType = messagingType;
        TestContext = testContext;

        PrimaryServices = new ServiceCollection();
        PrimaryServices.AddSingleton(Configuration);
        PrimaryServices.AddLogging(builder =>
        {
            builder.AddConsole();
        });
        PrimaryServices.AddMemoryCache();
        PrimaryServices
            .AddMessagingRedisCache(options =>
            {
                Configuration.Bind("MessagingRedisCache", options);
                options.MessagingType = MessagingType;
            })
            .AddMemoryCacheSubscriber();

        SecondaryServices = new ServiceCollection();
        SecondaryServices.AddSingleton(Configuration);
        SecondaryServices.AddLogging(builder =>
        {
            builder.AddConsole();
        });
        SecondaryServices.AddMemoryCache();
        SecondaryServices
            .AddMessagingRedisCache(options =>
            {
                Configuration.Bind("MessagingRedisCache", options);
                options.MessagingType = MessagingType;
            })
            .AddMemoryCacheSubscriber();
    }

    public virtual Action<MessagingRedisCacheOptions>? PrimaryOptionsConfigureAction { get; set; }
    public virtual Action<MessagingRedisCacheOptions>? SecondaryOptionsConfigureAction { get; set; }

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
    public TestContext TestContext { get; }

    protected IServiceCollection PrimaryServices { get; private set; } = default!;
    protected IServiceCollection SecondaryServices { get; private set; } = default!;

    private AutoResetEvent PrimarySubscribeAutoResetEvent { get; } =
        new(false);
    private AutoResetEvent SecondarySubscribeAutoResetEvent { get; } =
        new(false);

    [TestInitialize]
    public virtual void TestInitialize()
    {
        if (PrimaryOptionsConfigureAction != null)
        {
            PrimaryServices
                .Configure(
                    PrimaryOptionsConfigureAction);
        }
        PrimaryServices.Configure<MessagingRedisCacheOptions>(
            options =>
            {
                options.Events.OnSubscribe = () =>
                {
                    PrimarySubscribeAutoResetEvent.Set();
                    return Task.CompletedTask;
                };
            });
        PrimaryServiceProvider = PrimaryServices
            .BuildServiceProvider();
        PrimaryDistributedCache = PrimaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        PrimaryMemoryCache = PrimaryServiceProvider
            .GetRequiredService<IMemoryCache>();

        if (SecondaryOptionsConfigureAction != null)
        {
            SecondaryServices
                .Configure(
                    SecondaryOptionsConfigureAction);
        }
        SecondaryServices.Configure<MessagingRedisCacheOptions>(
            options =>
            {
                options.Events.OnSubscribe = () =>
                {
                    SecondarySubscribeAutoResetEvent.Set();
                    return Task.CompletedTask;
                };
            });
        SecondaryServiceProvider = SecondaryServices
            .BuildServiceProvider();
        SecondaryDistributedCache = SecondaryServiceProvider
            .GetRequiredService<IDistributedCache>();
        SecondaryMemoryCache = SecondaryServiceProvider
            .GetRequiredService<IMemoryCache>();

        PrimarySubscribeAutoResetEvent
            .WaitOne(EventTimeout);
        SecondarySubscribeAutoResetEvent
            .WaitOne(EventTimeout);
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
                    messagingRedisCache.Database.Value,
                    cancellationToken: TestContext.CancellationToken)
                .ConfigureAwait(false));
    }
}
