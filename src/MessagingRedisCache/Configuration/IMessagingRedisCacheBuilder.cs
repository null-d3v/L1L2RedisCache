using Microsoft.Extensions.DependencyInjection;

namespace MessagingRedisCache;

/// <summary>
/// A builder abstraction for configuring a messaging Redis cache.
/// </summary>
public interface IMessagingRedisCacheBuilder
{
    IServiceCollection Services { get; }
}