using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddL1L2DistributedRedisCache(
            this IServiceCollection services,
            Action<RedisCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            var options = new RedisCacheOptions();
            setupAction.Invoke(options);

            services.AddOptions();
            services.Configure(setupAction);
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(options.Configuration));
            services.AddMemoryCache();
            services.AddSingleton<RedisCache, RedisCache>();
            services.AddSingleton<IDistributedCache, L1L2RedisCache.L1L2RedisCache>();

            return services;
        }
    }
}