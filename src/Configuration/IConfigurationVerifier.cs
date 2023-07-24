namespace L1L2RedisCache;

/// <summary>
/// Verifies Redis configuration settings.
/// </summary>
public interface IConfigurationVerifier
{
    /// <summary>
    /// Verifies Redis configuration values.
    /// </summary>
    /// <param name="config">The configuration key.</param>
    /// <param name="cancellationToken">Optional. The System.Threading.CancellationToken used to propagate notifications that the operation should be canceled.</param>
    /// <param name="expectedValues">The expected values of the configuration.</param>
    Task<bool> VerifyConfigurationAsync(
        string config,
        CancellationToken cancellationToken = default,
        params string[] expectedValues);
}
