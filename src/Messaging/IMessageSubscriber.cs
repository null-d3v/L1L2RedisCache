namespace L1L2RedisCache;

/// <summary>
/// Subscribes to messages published by other L1L2RedisCache instances indicating cache values have changed.
/// </summary>
public interface IMessageSubscriber
{
    /// <summary>
    /// Subscribes to messages indicating cache values have changed.
    /// </summary>
    Task SubscribeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes to messages indicating cache values have changed.
    /// </summary>
    Task UnsubscribeAsync(
        CancellationToken cancellationToken = default);
}