using Microsoft.Extensions.Options;

namespace L1L2RedisCache;

internal sealed class ConfigurationVerifier :
    IConfigurationVerifier
{
    public ConfigurationVerifier(
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; set; }

    public async Task<bool> VerifyConfigurationAsync(
        string config,
        CancellationToken _ = default,
        params string[] expectedValues)
    {
        var isVerified = true;

        var database = (await L1L2RedisCacheOptions
            .ConnectionMultiplexerFactory!()
            .ConfigureAwait(false))
            .GetDatabase(
                L1L2RedisCacheOptions
                    .ConfigurationOptions?
                    .DefaultDatabase ?? -1) ??
                throw new InvalidOperationException();

        var configValue = (await database
            .ExecuteAsync(
                "config",
                "get",
                config)
            .ConfigureAwait(false))
            .ToDictionary()[config]
            .ToString();
        foreach (var expectedValue in expectedValues)
        {
            if (configValue?.Contains(
                    expectedValue,
                    StringComparison.Ordinal) != true)
            {
                isVerified = false;
            }
        }

        return isVerified;
    }
}
