using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace L1L2RedisCache;

/// <summary>
/// Configuration options for <c>L1L2RedisCache</c>.
/// </summary>
public class L1L2RedisCacheOptions :
    RedisCacheOptions, IOptions<L1L2RedisCacheOptions>
{
    /// <summary>
    /// Initializes a new instance of L1L2RedisCacheOptions.
    /// </summary>
    public L1L2RedisCacheOptions() : base()
    {
    }

    /// <summary>
    /// Unique identifier for the operating instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The pub/sub channel name.
    /// </summary>
    public string Channel { get { return $"{KeyPrefix}Channel"; } }

    /// <summary>
    /// A prefix to be applied to all cache keys.
    /// </summary>
    public string KeyPrefix { get { return InstanceName; } }

    /// <summary>
    /// A prefix to be applied to all L1 lock cache keys.
    /// </summary>
    public string LockKeyPrefix { get { return $"{Id}:{KeyPrefix}"; } }

    /// <summary>
    /// The type of messaging to use for L1 memory cache eviction.
    /// </summary>
    public MessagingType MessagingType { get; set; } =
        MessagingType.Default;

    L1L2RedisCacheOptions IOptions<L1L2RedisCacheOptions>.Value
    {
        get { return this; }
    }
}
