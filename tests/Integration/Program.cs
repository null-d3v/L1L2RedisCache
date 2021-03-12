
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace L1L2RedisCache.Test.Integration
{
    public class Program
    {
        static Program()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .Build();

            Services = new ServiceCollection();

            Services.AddL1L2DistributedRedisCache(options =>
            {
                options.Configuration = "localhost";
                options.InstanceName = "L1L2RedisCache.Test.";
            });
        }

        public static IConfigurationRoot Configuration { get; }
        public static IServiceCollection Services { get; }

        public static async Task<int> Main(string[] args)
        {
            var serviceProvider = Services.BuildServiceProvider();

            var l1l2Cache = serviceProvider
                .GetService<IDistributedCache>();
            var l2Cache = serviceProvider
                .GetService<Func<IDistributedCache>>()?.Invoke();

            await BasicPerformanceTest(l1l2Cache, l2Cache, 10000);
            Console.WriteLine();

            await ParallelPerformanceTest(l1l2Cache, l2Cache, 10000);

            return 1;
        }

        private static async Task BasicPerformanceTest(
            IDistributedCache l1l2Cache,
            IDistributedCache l2Cache,
            int count)
        {
            var stopWatch = new Stopwatch();

            Console.WriteLine($"Starting basic performance test: {count}");

            Console.WriteLine("Starting seed");
            stopWatch.Start();
            for (int index = 0; index < count; index++)
            {
                await l2Cache.SetStringAsync(
                    $"key{index}",
                    "value",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    });
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks to populate");

            Console.WriteLine("Starting L2 get test");
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                await l2Cache.GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L2 get test");

            Console.WriteLine("Starting L1 propagation test");
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                await l1l2Cache.GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L1 propagation test");

            Console.WriteLine("Starting L1L2 get test");
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                await l1l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L1L2 get test");
        }

        private static async Task ParallelPerformanceTest(
            IDistributedCache l1l2Cache,
            IDistributedCache l2Cache,
            int count)
        {
            var stopWatch = new Stopwatch();

            Console.WriteLine($"Starting parallel performance test: {count}");

            Console.WriteLine("Starting seed");
            stopWatch.Start();
            for (int index = 0; index < count; index++)
            {
                await l2Cache.SetStringAsync(
                    $"key{index}",
                    "value",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    });
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks to populate");

            Console.WriteLine("Starting L2 get test");
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                await l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L2 get test");

            Console.WriteLine("Starting parallel L1 propagation test");
            ThreadPool.SetMaxThreads(1000, 1000);
            var tasks = new List<Task>();
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                tasks.Add(l1l2Cache.GetStringAsync($"key{index}"));
            }
            await Task.WhenAll(tasks);
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for parallel L1 propagation test");

            Console.WriteLine("Starting L1L2 get test");
            stopWatch.Restart();
            for (int index = 0; index < count; index++)
            {
                await l1l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L1L2 get test");
        }
    }
}
