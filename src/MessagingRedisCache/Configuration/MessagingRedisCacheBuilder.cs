using Microsoft.Extensions.DependencyInjection;

namespace MessagingRedisCache;

internal class MessagingRedisCacheBuilder(
    IServiceCollection services) :
    IMessagingRedisCacheBuilder
{
    public IServiceCollection Services { get; } =
        services;
}