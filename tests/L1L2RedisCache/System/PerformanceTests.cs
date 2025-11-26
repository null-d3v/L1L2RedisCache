using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L1L2RedisCache.Tests.System;

[TestClass]
public class PerformanceTests
{
    private IConfig Config { get; } =
        DefaultConfig.Instance.WithOptions(
            ConfigOptions.DisableOptimizationsValidator);

    [TestMethod]
    public void GetPerformanceTest()
    {
        var benchmarkSummary = BenchmarkRunner
            .Run<GetBenchmark>(Config);

        Assert.IsTrue(
            benchmarkSummary.Reports.All(
                r => r.Success));

        var l1GetReport = benchmarkSummary
            .GetReportFor<GetBenchmark>(
                gB => gB.L1Get());
        var l1L2GetReport = benchmarkSummary
            .GetReportFor<GetBenchmark>(
                gB => gB.L1L2Get());
        var l1L2GetPropagationReport = benchmarkSummary
            .GetReportFor<GetBenchmark>(
                gB => gB.L1L2GetPropagation());
        var l2GetReport = benchmarkSummary
            .GetReportFor<GetBenchmark>(
                gB => gB.L2Get());

        var l2GetVsL1L2GetRatio =
            l2GetReport.ResultStatistics?.Median /
                l1L2GetReport.ResultStatistics?.Median ?? 0;
        Assert.IsGreaterThan(
            100,
            l2GetVsL1L2GetRatio,
            $"L1L2RedisCache Get must perform significantly better (> 100) than RedisCache Get: {l2GetVsL1L2GetRatio}");

        var l1L2GetPropagationVsl2GetRatio =
            l1L2GetPropagationReport.ResultStatistics?.Median /
                l2GetReport.ResultStatistics?.Median ?? 0;
        Assert.IsLessThan(
            3,
            l1L2GetPropagationVsl2GetRatio,
            $"L1L2RedisCache GetPropagation cannot perform significantly worse (< 3) than RedisCache Get: {l1L2GetPropagationVsl2GetRatio}");
    }

    [TestMethod]
    public void SetPerformanceTest()
    {
        var benchmarkSummary = BenchmarkRunner
            .Run<SetBenchmark>(Config);

        Assert.IsTrue(
            benchmarkSummary.Reports.All(
                r => r.Success));

        var l1L2SetReport = benchmarkSummary
            .GetReportFor<SetBenchmark>(
                gB => gB.L1L2Set());
        var l2SetReport = benchmarkSummary
            .GetReportFor<SetBenchmark>(
                gB => gB.L2Set());

        var l1L2SetVsl2SetRatio =
            l1L2SetReport.ResultStatistics?.Median /
                l2SetReport.ResultStatistics?.Median ?? 0;
        Assert.IsLessThan(
            3,
            l1L2SetVsl2SetRatio,
            $"L1L2RedisCache Set cannot perform significantly worse (< 3) than RedisCache Set: {l1L2SetVsl2SetRatio}");
    }
}