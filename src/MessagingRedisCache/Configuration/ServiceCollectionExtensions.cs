using MessagingRedisCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up messaging Redis cache related services in an Microsoft.Extensions.DependencyInjection.IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds messaging Redis caching services to the specified <c>IServiceCollection</c>.
    /// </summary>
    /// <returns>A <c>IMessagingRedisCacheBuilder</c> so that additional calls can be chained.</returns>
    [SuppressMessage("Performance", "CA1849")]
    public static IMessagingRedisCacheBuilder AddMessagingRedisCache(
        this IServiceCollection services,
        Action<MessagingRedisCacheOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddOptions();
        services.Configure(setupAction);
        services.Configure<MessagingRedisCacheOptions>(
            options =>
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
        services.TryAddSingleton<IDistributedCache, MessagingRedisCache.MessagingRedisCache>();
        services.TryAddSingleton<IMessagingConfigurationVerifier, MessagingConfigurationVerifier>();

        services.TryAddSingleton<DefaultMessagePublisher>();
        services.TryAddSingleton<NopMessagePublisher>();
        services.TryAddSingleton<IMessagePublisher>(
            serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<MessagingRedisCacheOptions>>()
                    .Value;

                return options.MessagingType switch
                {
                    MessagingType.Default =>
                        serviceProvider.GetRequiredService<DefaultMessagePublisher>(),
                    MessagingType.KeyeventNotifications or MessagingType.KeyspaceNotifications =>
                        serviceProvider.GetRequiredService<NopMessagePublisher>(),
                    _ =>
                        throw new NotImplementedException(),
                };
            });

        services.TryAddSingleton<IMessageSubscriber, NopMessageSubscriber>();

        return new MessagingRedisCacheBuilder(services);
    }
}
