namespace MessagingRedisCache;

/// <summary>
/// The type of messaging system to use for L1 memory cache eviction.
/// </summary>
public enum MessagingType
{
    /// <summary>
    /// Use standard <c>MessagingRedisCache</c> <see href="https://redis.io/topics/pubsub">pub/sub</see> messages for L1 memory cache eviction. The Redis server requires no additional configuration.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Use <see href="https://redis.io/topics/notifications">keyevent notifications</see> for L1 memory cache eviction instead of standard L1L2 <see href="https://redis.io/topics/pubsub">pub/sub</see> messages. The Redis server must have keyevent notifications enabled with at least <c>ghE</c> parameters.
    /// </summary>
    KeyeventNotifications = 1,

    /// <summary>
    /// Use <see href="https://redis.io/topics/notifications">keyspace notifications</see> for L1 memory cache eviction instead of standard L1L2 <see href="https://redis.io/topics/pubsub">pub/sub</see> messages. The Redis server must have keyevent notifications enabled with at least <c>ghK</c> parameters.
    /// </summary>
    KeyspaceNotifications = 2,
}
