# MessagingRedisCache

`MessagingRedisCache` is an implementation of [`IDistributedCache`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Caching.Abstractions/src/IDistributedCache.cs) based on [`RedisCache`](https://github.com/dotnet/aspnetcore/blob/main/src/Caching/StackExchangeRedis/src/RedisCache.cs). It will utilize [Redis pub/sub](https://redis.io/topics/pubsub) to ensure that cache entries can be synchronized in a distributed system, where direct deferral to Redis is not always performant. Because of this, it is a valuable backing store for [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid), and is used as the `IDistributedCache` for [`L1L2RedisCache`](lol.com).

All changes to entries in `MessagingRedisCache` by way of removal or updates, will result in a message being published. The default implementation is to innately publish these messages. Alternatively, [keyevent notifications](https://redis.io/topics/notifications) or [keyspace notifications](https://redis.io/topics/notifications) can be used if the Redis server is configured for them.

I expect to gracefully decomission this project when [`StackExchange.Redis`](https://github.com/StackExchange/StackExchange.Redis) has [client-side caching](https://redis.io/docs/latest/develop/use/client-side-caching/) support.

## Configuration

It is intended that `MessagingRedisCache` be used in conjunction with another kind of layered caching solution, specificially [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) or [`L1L2RedisCache`](lol.com). Without any kind of layered cache solution, `MessagingRedisCache` will only publish Redis pub/sub messages for another consumer.

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

This should be done explicitly when using [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) and is done automatically when using [`L1L2RedisCache`](lol.com).

## MessagingRedisCacheOptions

`MessagingRedisCacheOptions` are an extension of the standard `RedisCache` [`RedisCacheOptions`](https://github.com/dotnet/aspnetcore/blob/main/src/Caching/StackExchangeRedis/src/RedisCacheOptions.cs). The following additional customizations are supported:

### MessagingType

The type of message publishing system to use:

| MessagingType | Description | Suggestion |
| - | - | - |
| `Default` | Publish standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. | Default behavior. The Redis server requires no additional configuration. |
| `KeyeventNotifications` | Rely on [keyevent notifications](https://redis.io/topics/notifications) for message publishing instead of standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyevent notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghE` configuration and the majority of keys in the server are managed by `MessagingRedisCache`. |
| `KeyspaceNotifications` | Rely on [keyspace notifications](https://redis.io/topics/notifications) for message publishing instead of standard `MessagingRedisCache` [pub/sub](https://redis.io/topics/pubsub) messages. The Redis server must have keyspace notifications enabled. | This is only advisable if the Redis server is already using [keyevent notifications](https://redis.io/topics/notifications) with at least a `ghK` configuration and the majority of keys in the server are managed by `MessagingRedisCache`. |

