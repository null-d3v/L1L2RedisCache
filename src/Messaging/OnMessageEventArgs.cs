namespace L1L2RedisCache;

/// <summary>
/// Supplies information about a message event from an <c>IMessageSubscriber</c>.
/// </summary>
public class OnMessageEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of OnMessageEventArgs.
    /// </summary>
    public OnMessageEventArgs(
        string key)
    {
        Key = key;
    }

    /// <summary>
    /// The cache key pertaining to the message event.
    /// </summary>
    public string Key { get; set; }
}
