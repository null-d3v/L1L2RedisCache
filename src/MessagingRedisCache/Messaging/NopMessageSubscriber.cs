using StackExchange.Redis;

namespace MessagingRedisCache;

internal sealed class NopMessageSubscriber :
    IMessageSubscriber
{
    public Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
