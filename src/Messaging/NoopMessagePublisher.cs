namespace L1L2RedisCache;

internal class NoopMessagePublisher : IMessagePublisher
{
    public NoopMessagePublisher()
    {
    }

    public void Publish(string key)
    {
    }

    public Task PublishAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
