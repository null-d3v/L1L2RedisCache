using StackExchange.Redis;

namespace L1L2RedisCache;

internal sealed class NopMessagePublisher :
    IMessagePublisher
{
    public void Publish(
        IConnectionMultiplexer connectionMultiplexer,
        string key) { }

    public Task PublishAsync(
        IConnectionMultiplexer connectionMultiplexer,
        string key,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
