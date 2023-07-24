namespace L1L2RedisCache;

/// <summary>
/// A Redis pub/sub message indicating a cache value has changed.
/// </summary>
public class CacheMessage
{
    /// <summary>
    /// The cache key of the value that has changed.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// The unique publisher identifier of the cache that changed the value.
    /// </summary>
    public Guid PublisherId { get; set; }
}
