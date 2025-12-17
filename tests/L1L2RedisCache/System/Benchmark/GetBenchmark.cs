using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace L1L2RedisCache.Tests.System;

public class GetBenchmark : BenchmarkBase
{
    public new void GlobalSetup()
    {
        base.GlobalSetup();

        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1L2Cache!.SetString(
                $"Get:{iteration}",
                "Value",
                DistributedCacheEntryOptions);
            L1L2Cache!.SetString(
                $"GetPropagation:{iteration}",
                "Value",
                DistributedCacheEntryOptions);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1Cache!.Remove(
                $"GetPropagation:{iteration}");
        }
    }

    [Benchmark]
    public void L1Get()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1Cache!.Get(
                $"Get:{iteration}");
        }
    }

    [Benchmark]
    public void L1L2Get()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1L2Cache!.GetString(
                $"Get:{iteration}");
        }
    }

    [Benchmark]
    public void L1L2GetPropagation()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L1L2Cache!.GetString(
                $"GetPropagation:{iteration}");
        }
    }

    [Benchmark]
    public void L2Get()
    {
        for (var iteration = 1;
            iteration <= Iterations;
            iteration++)
        {
            L2Cache!.GetString(
                $"Get:{iteration}");
        }
    }
}