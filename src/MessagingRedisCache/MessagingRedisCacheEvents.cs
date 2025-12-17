using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// Specifies which events the <c>MessagingRedisCache</c> invokes.
/// </summary>
public class MessagingRedisCacheEvents
{
    /// <summary>
    /// Invoked when a message is recieved.
    /// </summary>
    public Func<ChannelMessage, Task> OnMessageRecieved { get; set; } =
        channelMessage => Task.CompletedTask;

    /// <summary>
    /// Invoked when a message subscription is established.
    /// </summary>
    public Func<Task> OnSubscribe { get; set; } =
        () => Task.CompletedTask;
}
