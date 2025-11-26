using MessagingRedisCache;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up L1L2RedisCache related services in an Microsoft.Extensions.DependencyInjection.IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(
        IServiceCollection services)
    {
        /// <summary>
        /// Adds L1L2RedisCache distributed caching services to the specified <c>IServiceCollection</c>.
        /// </summary>
        /// <returns>The <c>IServiceCollection</c> so that additional calls can be chained.</returns>
        public IServiceCollection AddL1L2RedisCache(
            Action<MessagingRedisCacheOptions> setupAction)
        {
            ArgumentNullException.ThrowIfNull(setupAction);

            services
                .AddMessagingRedisCache(setupAction)
                .AddMemoryCacheSubscriber();
            services.AddSingleton<IDistributedCache, L1L2RedisCache.L1L2RedisCache>();
            services.AddMemoryCache();

            return services;
        }
    }
}