using Microsoft.Extensions.Logging;

namespace MessagingRedisCache;

internal static partial class MessagingRedisCacheLoggerExtensions
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Redis notify-keyspace-events config is invalid for MessagingType {MessagingType}")]
    public static partial void MessagingConfigurationInvalid(
        this ILogger<MessagingRedisCache> logger,
        MessagingType messagingType,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to initialize subscriber; retrying in {SubscriberRetryDelay}")]
    public static partial void SubscriberFailed(
        this ILogger<MessagingRedisCache> logger,
        TimeSpan subscriberRetryDelay,
        Exception? exception = null);
}
