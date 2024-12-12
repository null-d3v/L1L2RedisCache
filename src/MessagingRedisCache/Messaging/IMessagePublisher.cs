using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// Publishes messages to other L1L2RedisCache instances indicating cache values have changed.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message indicating a cache value has changed.
    /// </summary>
    /// <param name="connectionMultiplexer">The <c>StackExchange.Redis.IConnectionMultiplexer</c> for publishing.</param>
    /// <param name="key">The cache key of the value that has changed.</param>
    void Publish(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key);

    /// <summary>
    /// Publishes a message indicating a cache value has changed.
    /// </summary>
    /// <param name="connectionMultiplexer">The <c>StackExchange.Redis.IConnectionMultiplexer</c> for publishing.</param>
    /// <param name="key">The cache key of the value that has changed.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    Task PublishAsync(
        IConnectionMultiplexer connectionMultiplexer,
        RedisKey key,
        CancellationToken cancellationToken = default);
}
