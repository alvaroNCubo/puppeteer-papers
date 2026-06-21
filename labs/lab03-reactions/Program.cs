using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;

namespace Lab03Reactions
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Non-BDN mode: one-shot Cue end-to-end latency + exactly-once probe,
            // measured with Stopwatch across a hot push loop. `cue <iters>`.
            if (args.Length >= 1 && args[0] == "cue")
            {
                int iters = args.Length >= 2 ? int.Parse(args[1]) : 2000;
                CueProbe.Run(iters);
                return;
            }

            // Steady-state job: tiered compilation disabled so the dynamically-emitted
            // compiled delegate is fully optimized from the first timed invocation,
            // removing Tier-0->Tier-1 bimodality. invocationCount pinned so the
            // in-memory journal stays bounded (iterationCount x invocationCount entries).
            var job = Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithWarmupCount(8)
                .WithIterationCount(15)
                .WithInvocationCount(2000)
                .WithUnrollFactor(1)
                .WithEnvironmentVariable("DOTNET_TieredCompilation", "0")
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithId("SteadyNoTiering");

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job)
                .AddColumn(StatisticColumn.Mean)
                .AddColumn(StatisticColumn.Error)
                .AddColumn(StatisticColumn.StdDev)
                .AddColumn(StatisticColumn.Median)
                .AddColumn(StatisticColumn.P95)
                .AddExporter(MarkdownExporter.GitHub)
                .WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default
                    .WithTimeUnit(TimeUnit.Microsecond));

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
