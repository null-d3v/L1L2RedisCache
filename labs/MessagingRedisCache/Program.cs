using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
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
