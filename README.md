# L1L2RedisCache

L1L2RedisCache is an implementation of [`IDistributedCache`](https://github.com/aspnet/Caching/blob/master/src/Microsoft.Extensions.Caching.Abstractions/IDistributedCache.cs) with a strong focus on performance. It leverages [`IMemoryCache`](https://github.com/aspnet/Caching/blob/master/src/Microsoft.Extensions.Caching.Abstractions/IMemoryCache.cs) as a level 1 cache and [`RedisCache`](https://github.com/aspnet/Caching/blob/master/src/Microsoft.Extensions.Caching.Redis/RedisCache.cs) as a level 2 cache, with level 1 evictions being managed via [Redis Pub/Sub](https://redis.io/topics/pubsub).

L1L2RedisCache is heavily inspired by development insights provided over the past several years by [StackOverflow](https://stackoverflow.com/). It attempts to simplify those concepts into a performant `IDistributedCache` that can be more generally applied.

## Use

L1L2RedisCache can be registered during startup with the following `IServiceCollection` extension method:

```
services.AddL1L2RedisCache(options =>
{
    options.Configuration = "localhost";
    options.InstanceName = "Namespace";
});
```

The `IOptions` used is [`RedisCacheOptions`](https://github.com/aspnet/Caching/blob/master/src/Microsoft.Extensions.Caching.Redis/RedisCacheOptions.cs), as there is a direct dependency on `Microsoft.Extensions.Caching.Redis`.

It is intended that L1L12RedisCache then be simply used via `IDistributedCache` dependency injection.

## Performance

L1L2RedisCache will generally outperform `RedisCache` when used with very high volume or very large cache entries. This can be attribtued to the non-reliance on deferring to Redis continually, reducing time spent because of latency, network traffic, and, very minimally, Redis operations.

## Caveats

Due to the complex nature of distributed L1, cache entries with sliding expirations are specifically stored only in L2.
