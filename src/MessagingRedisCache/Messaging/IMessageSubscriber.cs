using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// Subscribes to messages published by other L1L2RedisCache instances indicating cache values have changed.
/// </summary>
public interface IMessageSubscriber
{
    /// <summary>
    /// An event that is raised when a message is recieved.
    /// </summary>
    EventHandler<OnMessageEventArgs>? OnMessage { get; set; }

    /// <summary>
    /// An event that is raised when a subscription is created.
    /// </summary>
    EventHandler? OnSubscribe { get; set; }

    /// <summary>
    /// Subscribes to messages indicating cache values have changed.
    /// </summary>
    /// <param name="connectionMultiplexer">The <c>StackExchange.Redis.IConnectionMultiplexer</c> for subscribing.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    Task SubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes to messages indicating cache values have changed.
    /// </summary>
    /// <param name="connectionMultiplexer">The <c>StackExchange.Redis.IConnectionMultiplexer</c> for subscribing.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The System.Threading.Tasks.Task that represents the asynchronous operation.</returns>
    Task UnsubscribeAsync(
        IConnectionMultiplexer connectionMultiplexer,
        CancellationToken cancellationToken = default);
}
