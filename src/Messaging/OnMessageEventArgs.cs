namespace L1L2RedisCache;

/// <summary>
/// Supplies information about a message event from an <c>IMessageSubscriber</c>.
/// </summary>
/// <remarks>
/// Initializes a new instance of OnMessageEventArgs.
/// </remarks>
public class OnMessageEventArgs(
    string key) :
    EventArgs
{

    /// <summary>
    /// The cache key pertaining to the message event.
    /// </summary>
    public string Key { get; set; } = key;
}
