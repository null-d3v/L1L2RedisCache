using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal sealed class SubscriberHostedService :
    IHostedService
{
    private static readonly
        Action<ILogger, TimeSpan, Exception?> _subscriberFailed =
            LoggerMessage.Define<TimeSpan>(
                LogLevel.Warning,
                new EventId(0),
                "Failed to initialize subscriber; retrying in {SubscriberRetryDelay}");

    public SubscriberHostedService(
        IOptions<L1L2RedisCacheOptions> l1l2RedisCacheOptionsAccessor,
        IMessageSubscriber messageSubscriber,
        ILogger<SubscriberHostedService>? logger = null)
    {
        L1L2RedisCacheOptions = l1l2RedisCacheOptionsAccessor.Value;
        MessageSubscriber = messageSubscriber;

        Logger = logger;
    }

    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; }
    public ILogger<SubscriberHostedService>? Logger { get; set; }
    public IMessageSubscriber MessageSubscriber { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await MessageSubscriber
                    .SubscribeAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            catch (RedisConnectionException redisConnectionException)
            {
                if (Logger != null)
                {
                    _subscriberFailed(
                        Logger,
                        L1L2RedisCacheOptions
                            .SubscriberRetryDelay,
                        redisConnectionException);
                }

                await Task
                    .Delay(
                        L1L2RedisCacheOptions
                            .SubscriberRetryDelay,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await MessageSubscriber
            .UnsubscribeAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }
}
