
namespace L1L2RedisCache.Tests.System;

internal sealed class MemoryMessagePublisherSubscriber :
    IMessagePublisher,
    IMessageSubscriber
{
    public MemoryMessagePublisherSubscriber()
    {
        PublishedKeys = new List<string>();
    }

    public ICollection<string> PublishedKeys { get; }

    public void Publish(string key)
    {
        PublishedKeys.Add(key);
    }

    public Task PublishAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        PublishedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}