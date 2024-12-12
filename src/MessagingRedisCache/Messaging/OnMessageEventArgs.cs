using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// Supplies information about a message event from an <c>IMessageSubscriber</c>.
/// </summary>
/// <remarks>
/// Initializes a new instance of OnMessageEventArgs.
/// </remarks>
public class OnMessageEventArgs(
    RedisKey key) :
    EventArgs
{
    /// <summary>
    /// The cache key pertaining to the message event.
    /// </summary>
    public RedisKey Key { get; set; } = key;
}
