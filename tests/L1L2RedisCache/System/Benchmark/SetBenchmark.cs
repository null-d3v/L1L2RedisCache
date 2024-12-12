using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Distributed;

namespace L1L2RedisCache.Tests.System;

[SimpleJob]
public class SetBenchmark : BenchmarkBase
{
    [Benchmark]
    public void L1L2Set()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1L2Cache!.SetString(
                $"Set:{iteration}",
                "Value",
                DistributedCacheEntryOptions);
        }
    }

    [Benchmark]
    public void L2Set()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L2Cache!.SetString(
                $"Set:{iteration}",
                "Value",
                DistributedCacheEntryOptions);
        }
    }
}