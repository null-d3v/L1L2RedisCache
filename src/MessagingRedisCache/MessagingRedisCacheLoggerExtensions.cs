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
        Level = LogLevel.Warning,
        Message = "Messaging configuration cannot be automatically verified")]
    public static partial void MessagingConfigurationUnverified(
        this ILogger<MessagingRedisCache> logger,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Attempt {Attempt} to initialize subscriber")]
    public static partial void SubscribeAttempt(
        this ILogger<MessagingRedisCache> logger,
        int attempt,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to initialize subscriber; retrying in {SubscriberRetryDelay}")]
    public static partial void SubscribeAttemptFailed(
        this ILogger<MessagingRedisCache> logger,
        TimeSpan subscriberRetryDelay,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Subscriber was not initialized")]
    public static partial void SubscribeFailed(
        this ILogger<MessagingRedisCache> logger,
        Exception? exception = null);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Subscriber successfully initialized")]
    public static partial void SubscribeSucceeded(
        this ILogger<MessagingRedisCache> logger,
        Exception? exception = null);
}
