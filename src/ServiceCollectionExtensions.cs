using L1L2RedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up L1L2RedisCache related services in an Microsoft.Extensions.DependencyInjection.IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds L1L2RedisCache distributed caching services to the specified <c>IServiceCollection</c>.
    /// </summary>
    /// <returns>The <c>IServiceCollection</c> so that additional calls can be chained.</returns>
    [Obsolete("Use AddL1L2RedisCache instead.")]
    public static IServiceCollection AddL1L2DistributedRedisCache(
        this IServiceCollection services,
        Action<L1L2RedisCacheOptions> setupAction)
    {
        return AddL1L2RedisCache(services, setupAction);
    }

    /// <summary>
    /// Adds L1L2RedisCache distributed caching services to the specified <c>IServiceCollection</c>.
    /// </summary>
    /// <returns>The <c>IServiceCollection</c> so that additional calls can be chained.</returns>
    public static IServiceCollection AddL1L2RedisCache(
        this IServiceCollection services,
        Action<L1L2RedisCacheOptions> setupAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        if (setupAction == null)
        {
            throw new ArgumentNullException(nameof(setupAction));
        }

        var l1L2RedisCacheOptions = new L1L2RedisCacheOptions();
        setupAction.Invoke(l1L2RedisCacheOptions);

        services.AddOptions();
        services.Configure(setupAction);
        services.Configure<L1L2RedisCacheOptions>(
            (options) =>
            {
                if (options.ConnectionMultiplexerFactory == null)
                {
                    if (options.ConfigurationOptions != null)
                    {
                        options.ConnectionMultiplexerFactory = () =>
                            Task.FromResult(
                                ConnectionMultiplexer.Connect(
                                    options.ConfigurationOptions) as IConnectionMultiplexer);
                    }
                    else
                    {
                        options.ConnectionMultiplexerFactory = () =>
                            Task.FromResult(
                                ConnectionMultiplexer.Connect(
                                    options.Configuration) as IConnectionMultiplexer);
                    }
                }
            });
        services.AddMemoryCache();
        services.AddSingleton(
            provider => new Func<IDistributedCache>(
                () => new RedisCache(
                    provider.GetService<IOptions<L1L2RedisCacheOptions>>())));
        services.AddSingleton<IDistributedCache, L1L2RedisCache.L1L2RedisCache>();
        services.AddSingleton<IMessagePublisher, MessagePublisher>();
        services.AddSingleton<IMessageSubscriber, MessageSubscriber>();

        return services;
    }
}
