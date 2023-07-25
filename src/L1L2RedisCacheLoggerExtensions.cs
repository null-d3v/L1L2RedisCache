using Microsoft.Extensions.Logging;

namespace L1L2RedisCache;

internal static partial class L1L2RedisCacheLoggerExtensions
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Redis notify-keyspace-events config is invalid for MessagingType {MessagingType}")]
    public static partial void MessagingConfigurationInvalid(
        this ILogger<L1L2RedisCache> logger,
        MessagingType messagingType,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to initialize subscriber; retrying in {SubscriberRetryDelay}")]
    public static partial void SubscriberFailed(
        this ILogger<L1L2RedisCache> logger,
        TimeSpan subscriberRetryDelay,
        Exception? exception = null);
}
