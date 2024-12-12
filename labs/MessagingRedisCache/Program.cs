using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().AddFilter(null, LogLevel.Trace));
services.AddHybridCache();
services.AddMessagingRedisCache(options =>
{
    options.Configuration = "redis";
    options.InstanceName = "MessagingRedisCache:Test:";
});
var serviceProvider = services.BuildServiceProvider();

var hybridCache = serviceProvider
    .GetRequiredService<HybridCache>();

var key = Guid.NewGuid().ToString();
var value = Guid.NewGuid().ToString();

await hybridCache
    .SetAsync(
        key,
        value,
        new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(1),
        })
    .ConfigureAwait(false);

var testValue = await hybridCache
    .GetOrCreateAsync(
        key,
        cancellationToken =>
            ValueTask.FromResult<string?>(null))
    .ConfigureAwait(false);

var test = "";
