using StackExchange.Redis;

namespace MessagingRedisCache;

internal sealed class NopMessageSubscriber :
    IMessageSubscriber
{
    public EventHandler<OnMessageEventArgs>? OnMessage { get; set; }
    public EventHandler? OnSubscribe { get; set; }

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
