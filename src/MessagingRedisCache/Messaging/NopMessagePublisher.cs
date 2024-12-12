using StackExchange.Redis;

namespace MessagingRedisCache;

internal sealed class NopMessagePublisher :
    IMessagePublisher
{
    public void Publish(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key) { }

    public Task PublishAsync(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
