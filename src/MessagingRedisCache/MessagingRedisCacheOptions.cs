﻿using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace MessagingRedisCache;

/// <summary>
/// Configuration options for <c>L1L2RedisCache</c>.
/// </summary>
public class MessagingRedisCacheOptions() :
    RedisCacheOptions()
{
    /// <summary>
    /// The pub/sub channel name.
    /// </summary>
    public string Channel =>
        $"{InstanceName}{nameof(MessagingRedisCache)}";

    /// <summary>
    /// Unique identifier for the operating instance.
    /// </summary>
    public Guid Id { get; } =
        Guid.NewGuid();

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
}
