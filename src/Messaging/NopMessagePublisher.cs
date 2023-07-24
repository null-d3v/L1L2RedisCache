namespace L1L2RedisCache;

internal sealed class NopMessagePublisher :
    IMessagePublisher
{
    public NopMessagePublisher() { }

    public void Publish(string key) { }

    public Task PublishAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
