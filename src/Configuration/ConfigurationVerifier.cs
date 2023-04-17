using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

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

    [SuppressMessage("Design", "CA1031")]
    public bool TryVerifyConfiguration(
        string config,
        [MaybeNullWhen(true)] out Exception? error,
        params string[] expectedValues)
    {
        error = null;
        var verified = true;

        try
        {
            var database = L1L2RedisCacheOptions
                .ConnectionMultiplexerFactory?
                .Invoke()
                .GetAwaiter()
                .GetResult()
                .GetDatabase(
                    L1L2RedisCacheOptions
                        .ConfigurationOptions?
                        .DefaultDatabase ?? -1) ??
                throw new InvalidOperationException();

            var configValue = database
                .Execute(
                    "config",
                    "get",
                    config)
                .ToDictionary()[config]
                .ToString();
            foreach (var expectedValue in expectedValues)
            {
                if (configValue?.Contains(
                        expectedValue,
                        StringComparison.Ordinal) != true)
                {
                    verified = false;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception;
            verified = false;
        }

        return verified;
    }
}
