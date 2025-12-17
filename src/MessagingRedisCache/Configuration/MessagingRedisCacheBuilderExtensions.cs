using MessagingRedisCache;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up messaging Redis cache related services.
/// </summary>
public static class MessagingRedisCacheBuilderExtensions
{
    extension(
        IMessagingRedisCacheBuilder builder)
    {
        public IMessagingRedisCacheBuilder AddMemoryCacheSubscriber()
        {
            builder.Services.TryAddSingleton<DefaultMessageSubscriber>();
            builder.Services.TryAddSingleton<KeyeventMessageSubscriber>();
            builder.Services.TryAddSingleton<KeyspaceMessageSubscriber>();
            builder.Services.AddSingleton<IMessageSubscriber>(
                serviceProvider =>
                {
                    var options = serviceProvider
                        .GetRequiredService<IOptions<MessagingRedisCacheOptions>>()
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

            return builder;
        }
    }
}