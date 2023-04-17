using System.Diagnostics.CodeAnalysis;

namespace L1L2RedisCache;

/// <summary>
/// Attempts to verify Redis configuration settings.
/// </summary>
public interface IConfigurationVerifier
{
    /// <summary>
    /// Attempts to verify Redis configuration values.
    /// </summary>
    /// <param name="config">The configuration key.</param>
    /// <param name="error">The exception that occurred during verification, if any.</param>
    /// <param name="expectedValues">The expected values of the configuration.</param>
    [SuppressMessage("Naming", "CA1716")]
    bool TryVerifyConfiguration(
        string config,
        [MaybeNullWhen(true)] out Exception? error,
        params string[] expectedValues);
}
