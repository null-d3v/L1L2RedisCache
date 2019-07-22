
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace L1L2RedisCache.Test.Integration
{
    public class Program
    {
        static Program()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
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

        public async static Task<int> Main(string[] args)
        {
            var serviceProvider = Services.BuildServiceProvider();

            var l1l2Cache = serviceProvider
                .GetService<IDistributedCache>();
            var l2Cache = serviceProvider
                .GetService<Func<IDistributedCache>>()();

            var stopWatch = new Stopwatch();
            
            Console.WriteLine("Starting seed");
            stopWatch.Start();
            for (int index = 0; index < 1000; index++)
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
            for (int index = 0; index < 1000; index++)
            {
                var value = await l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L2 get test");

            Console.WriteLine("Starting L1 propagation test");
            stopWatch.Restart();
            for (int index = 0; index < 1000; index++)
            {
                var value = await l1l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L1 propagation test");

            Console.WriteLine("Starting L1L2 get test");
            stopWatch.Restart();
            for (int index = 0; index < 1000; index++)
            {
                var value = await l1l2Cache
                    .GetStringAsync($"key{index}");
            }
            stopWatch.Stop();
            Console.WriteLine($"{stopWatch.ElapsedTicks} ticks for L1 get test");

            return 1;
        }
    }
}
