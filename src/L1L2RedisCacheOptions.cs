using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace L1L2RedisCache;

/// <summary>
/// Configuration options for <c>L1L2RedisCache</c>.
/// </summary>
public sealed class L1L2RedisCacheOptions :
    RedisCacheOptions, IOptions<L1L2RedisCacheOptions>
{
    /// <summary>
    /// Initializes a new instance of L1L2RedisCacheOptions.
    /// </summary>
    public L1L2RedisCacheOptions() : base() { }

    /// <summary>
    /// Unique identifier for the operating instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The pub/sub channel name.
    /// </summary>
    public string Channel => $"{KeyPrefix}Channel";

    /// <summary>
    /// A prefix to be applied to all cache keys.
    /// </summary>
    public string KeyPrefix => InstanceName ?? string.Empty;

    /// <summary>
    /// A prefix to be applied to all L1 lock cache keys.
    /// </summary>
    public string LockKeyPrefix => $"{KeyPrefix}{Id}";

    /// <summary>
    /// The type of messaging to use for L1 memory cache eviction.
    /// </summary>
    public MessagingType MessagingType { get; set; } =
        MessagingType.Default;

    /// <summary>
    /// The duration of time to delay before retrying subscriber intialization.
    /// </summary>
    public TimeSpan SubscriberRetryDelay { get; set; } =
        TimeSpan.FromSeconds(5);

    L1L2RedisCacheOptions IOptions<L1L2RedisCacheOptions>.Value => this;
}
