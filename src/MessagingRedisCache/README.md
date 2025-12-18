# MessagingRedisCache

`MessagingRedisCache` is an implementation of [`IDistributedCache`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Caching.Abstractions/src/IDistributedCache.cs) using [`RedisCache`](https://github.com/dotnet/aspnetcore/blob/main/src/Caching/StackExchangeRedis/src/RedisCache.cs) as a base implementation. `MessagingRedisCache` will utilize [Redis pub/sub](https://redis.io/topics/pubsub) to ensure that memory cache entries can be synchronized in a distributed system. This makes it a viable backing store for [`HybridCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid), where it can evict `IMemoryCache` entries in distributed systems.

All changes to entries in `MessagingRedisCache` by way of removal or updates will result in a Redis message being published. The default implementation is to directly publish these messages in an established channel. Alternatively, [keyevent notifications](https://redis.io/topics/notifications) or [keyspace notifications](https://redis.io/topics/notifications) can be used if the Redis server is configured for them.

I expect to gracefully decomission this project when [`StackExchange.Redis`](https://github.com/StackExchange/StackExchange.Redis) has [client-side caching](https://redis.io/docs/latest/develop/use/client-side-caching/) support or if [`HybridCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) similarly implements client-side eviction.

## Configuration

It is intended that `MessagingRedisCache` be used in conjunction with another kind of layered caching solution, specificially [`HybridCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) or [`L1L2RedisCache`](../L1L2RedisCache/README.md). Without any kind of layered cache solution, `MessagingRedisCache` will only publish Redis pub/sub messages for another consumer unconfigured by this project.

`MessagingRedisCache` can be registered during startup with the following `IServiceCollection` extension method:

```
services
    .AddMessagingRedisCache(options =>
    {
        options.Configuration = "redis";
        options.InstanceName = "Namespace:Prefix:";
    });
```

### Message Subscription

A message subscriber can be registered on the `IMessagingRedisCacheBuilder` which will automatically evict cache entries from an `IMemoryCache` registered on the `IServiceCollection`:

```
services
    .AddMessagingRedisCache(options =>
    {
        options.Configuration = "redis";
        options.InstanceName = "Namespace:Prefix:";
    })
    .AddMemoryCacheSubscriber();
```

This is done automatically when using [`L1L2RedisCache`](../L1L2RedisCache/README.md), but should be done explicitly when using [`HybridCache`](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid). A complete `HybridCache` configuration could appear as such:

```
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
```

## MessagingRedisCacheOptions

`MessagingRedisCacheOptions` are an extension of the standard [`RedisCacheOptions`](https://github.com/dotnet/aspnetcore/blob/main/src/Caching/StackExchangeRedis/src/RedisCacheOptions.cs). The following additional customizations are supported:

### MessagingType

The type of message publishing system to use:

| MessagingType | Description | Suggestion |
| - | - | - |
| `Default` | Publish standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. | Default behavior. The Redis server requires no additional configuration. |
| `KeyeventNotifications` | Rely on [keyevent notifications](https://redis.io/topics/notifications) for message publishing instead of standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyevent notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghE` configuration and the majority of keys in the server are managed by `MessagingRedisCache`. |
| `KeyspaceNotifications` | Rely on [keyspace notifications](https://redis.io/topics/notifications) for message publishing instead of standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyspace notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghK` configuration and the majority of keys in the server are managed by `MessagingRedisCache`. |
