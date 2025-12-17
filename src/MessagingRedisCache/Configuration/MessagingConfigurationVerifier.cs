using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MessagingRedisCache;

public sealed class MessagingConfigurationVerifier(
    IOptions<MessagingRedisCacheOptions> messagingRedisCacheOptionsAccessor) :
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

    public static IDictionary<MessagingType, string> NotifyKeyspaceEventsConfig { get; }

    public MessagingRedisCacheOptions MessagingRedisCacheOptions { get; } =
        messagingRedisCacheOptionsAccessor.Value;

    public async Task<bool> VerifyConfigurationAsync(
        IDatabase database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        var isVerified = NotifyKeyspaceEventsConfig
            .TryGetValue(
                MessagingRedisCacheOptions.MessagingType,
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
