using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace L1L2RedisCache;

internal static class HashEntryArrayExtensions
{
    private const string AbsoluteExpirationKey = "absexp";
    private const string DataKey = "data";
    private const long NotPresent = -1;
    private const string SlidingExpirationKey = "sldexp";

    internal static DistributedCacheEntryOptions GetDistributedCacheEntryOptions(
        this HashEntry[] hashEntries)
    {
        var distributedCacheEntryOptions = new DistributedCacheEntryOptions();

        var absoluteExpirationHashEntry = hashEntries.FirstOrDefault(
            hashEntry => hashEntry.Name == AbsoluteExpirationKey);
        if (absoluteExpirationHashEntry.Value.HasValue &&
            absoluteExpirationHashEntry.Value != NotPresent)
        {
            distributedCacheEntryOptions.AbsoluteExpiration = new DateTimeOffset(
                (long)absoluteExpirationHashEntry.Value, TimeSpan.Zero);
        }

        var slidingExpirationHashEntry = hashEntries.FirstOrDefault(
            hashEntry => hashEntry.Name == SlidingExpirationKey);
        if (slidingExpirationHashEntry.Value.HasValue &&
            slidingExpirationHashEntry.Value != NotPresent)
        {
            distributedCacheEntryOptions.SlidingExpiration = new TimeSpan(
                (long)slidingExpirationHashEntry.Value);
        }

        return distributedCacheEntryOptions;
    }

    internal static RedisValue GetRedisValue(
        this HashEntry[] hashEntries)
    {
        var dataHashEntry = hashEntries.FirstOrDefault(
            hashEntry => hashEntry.Name == DataKey);

        return dataHashEntry.Value;
    }
}