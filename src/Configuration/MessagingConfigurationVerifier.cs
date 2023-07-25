using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal sealed class MessagingConfigurationVerifier :
    IMessagingConfigurationVerifier
{
    private const string config = "notify-keyspace-events";

    static MessagingConfigurationVerifier()
    {
        NotifyKeyspaceEventsConfig = new Dictionary<MessagingType, string>
        {
            { MessagingType.Default, string.Empty },
            { MessagingType.KeyeventNotifications, "ghE" },
            { MessagingType.KeyspaceNotifications, "ghK" },
        };
    }

    public MessagingConfigurationVerifier(
        IOptions<L1L2RedisCacheOptions> l1L2RedisCacheOptionsOptionsAccessor)
    {
        L1L2RedisCacheOptions = l1L2RedisCacheOptionsOptionsAccessor.Value;
    }

    internal static IDictionary<MessagingType, string> NotifyKeyspaceEventsConfig { get; }

    public L1L2RedisCacheOptions L1L2RedisCacheOptions { get; }

    public async Task<bool> VerifyConfigurationAsync(
        IDatabase database,
        CancellationToken _ = default)
    {
        var isVerified = NotifyKeyspaceEventsConfig
            .TryGetValue(
                L1L2RedisCacheOptions.MessagingType,
                out var expectedValues);

        var configValue = (await database
            .ExecuteAsync(
                "config",
                "get",
                config)
            .ConfigureAwait(false))
            .ToDictionary()[config]
            .ToString();

        if (expectedValues != null)
        {
            foreach (var expectedValue in expectedValues)
            {
                if (configValue?.Contains(
                        expectedValue,
                        StringComparison.Ordinal) != true)
                {
                    isVerified = false;
                }
            }
        }

        return isVerified;
    }
}
