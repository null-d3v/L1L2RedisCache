using L1L2RedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

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
    [SuppressMessage("Performance", "CA1849")]
    public static IServiceCollection AddL1L2RedisCache(
        this IServiceCollection services,
        Action<L1L2RedisCacheOptions> setupAction)
    {
        if (setupAction == null)
        {
            throw new ArgumentNullException(
                nameof(setupAction));
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
                    else if (!string.IsNullOrEmpty(options.Configuration))
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
            serviceProvider => new Func<IDistributedCache>(
                () => new RedisCache(
                    serviceProvider.GetRequiredService<IOptions<L1L2RedisCacheOptions>>())));
        services.AddSingleton<IDistributedCache, L1L2RedisCache.L1L2RedisCache>();
        services.AddSingleton<IConfigurationVerifier, ConfigurationVerifier>();

        services.AddSingleton<DefaultMessagePublisher>();
        services.AddSingleton<NopMessagePublisher>();
        services.AddSingleton<IMessagePublisher>(
            serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
                    .Value;

                return options.MessagingType switch
                {
                    MessagingType.Default =>
                        serviceProvider.GetRequiredService<DefaultMessagePublisher>(),
                    _ =>
                        serviceProvider.GetRequiredService<NopMessagePublisher>(),
                };
            });
        services.AddHostedService<SubscriberHostedService>();

        services.AddSingleton<DefaultMessageSubscriber>();
        services.AddSingleton<KeyeventMessageSubscriber>();
        services.AddSingleton<KeyspaceMessageSubscriber>();
        services.AddSingleton<IMessageSubscriber>(
            serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<L1L2RedisCacheOptions>>()
                    .Value;

                return options.MessagingType switch
                {
                    MessagingType.Default =>
                        serviceProvider.GetRequiredService<DefaultMessageSubscriber>(),
                    MessagingType.KeyeventNotifications =>
                        serviceProvider.GetRequiredService<KeyeventMessageSubscriber>(),
                    MessagingType.KeyspaceNotifications =>
                        serviceProvider.GetRequiredService<KeyspaceMessageSubscriber>(),
                    _ => throw new NotImplementedException(),
                };
            });

        return services;
    }
}
