# L1L2RedisCache

`L1L2RedisCache` is an implementation of [`IDistributedCache`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Caching.Abstractions/src/IDistributedCache.cs), leveraging [`IMemoryCache`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Caching.Abstractions/src/IMemoryCache.cs) as a level 1 cache and [`MessagingRedisCache`](../MessagingRedisCache/README.md) as a level 2 cache. Level 1 evictions are managed via `MessagingRedisCache`'s [Redis pub/sub](https://redis.io/topics/pubsub).

`L1L2RedisCache` is heavily inspired by development insights provided over the past several years by [StackOverflow](https://stackoverflow.com/). It attempts to simplify those concepts into a highly accessible `IDistributedCache` implementation that is more performant than a baseline `RedisCache`.

Using a [`HybridCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) combined with [`MessagingRedisCache`](../MessagingRedisCache/README.md) is a viable alternative to this project. It is possible that with increased functionality of `HybridCache` that this project will no longer be necessary.

## Configuration

It is intended that L1L12RedisCache be used as an `IDistributedCache` implementation.

`L1L2RedisCache` can be registered during startup with the following `IServiceCollection` extension method:

```
services.AddL1L2RedisCache(options =>
{
    options.Configuration = "localhost";
    options.InstanceName = "Namespace:Prefix:";
});
```

## MessagingRedisCacheOptions

`L1L2RedisCache` uses `MessagingRedisCacheOptions` which extend the standard `RedisCache` [`RedisCacheOptions`](https://github.com/dotnet/aspnetcore/blob/main/src/Caching/StackExchangeRedis/src/RedisCacheOptions.cs). The following additional customizations are supported:

### MessagingType

The type of messaging system to use for L1 memory cache eviction.

| MessagingType | Description | Suggestion |
| - | - | - |
| `Default` | Use standard `L1L2RedisCache` [pub/sub](https://redis.io/topics/pubsub) messages for L1 memory cache eviction. | Default behavior. The Redis server requires no additional configuration. |
| `KeyeventNotifications` | Use [keyevent notifications](https://redis.io/topics/notifications) for L1 memory eviction instead of standard `L1L2RedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyevent notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghE` configuration and the majority of keys in the server are managed by `L1L2RedisCache`. |
| `KeyspaceNotifications` | Use [keyspace notifications](https://redis.io/topics/notifications) for L1 memory eviction instead of standard `L1L2RedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyspace notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghK` configuration and the majority of keys in the server are managed by `L1L2RedisCache`. |

## Performance

L1L2RedisCache will generally outperform a baseline `RedisCache`, particularly in cases of high volume or large cache entries. As entries are opportunistically pulled from memory instead of Redis, where costs of latency, network, and Redis operations are avoided. Respective performance gains will rely heavily on the impact of the afforementioned factors.

## Considerations

Due to the complex nature of a distributed L1 memory cache, cache entries with sliding expirations are only stored in L2 (Redis). These entries will show no performance improvement over the standard `RedisCache`, but incur no performance penalty.
