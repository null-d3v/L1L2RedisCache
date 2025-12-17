using StackExchange.Redis;

namespace MessagingRedisCache;

/// <summary>
/// Verifies Redis configuration settings.
/// </summary>
public interface IMessagingConfigurationVerifier
{
    /// <summary>
    /// Verifies Redis configuration values.
    /// </summary>
    /// <param name="database">The <c>StackExchange.Redis.IDatabase</c> for configuration values.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    Task<bool> VerifyConfigurationAsync(
        IDatabase database,
        CancellationToken cancellationToken = default);
}
