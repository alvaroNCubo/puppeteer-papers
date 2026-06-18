using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

namespace BenchPaper2Bdn
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Non-BDN mode: one-shot synthetic sweeps (compile-by-depth, eval-complexity)
            // measured via the LabInstrumentation hooks. `synthetic <outDir> <sha>`.
            if (args.Length >= 1 && args[0] == "synthetic")
            {
                var outDir = args.Length >= 2 ? args[1] : ".";
                var shaArg = args.Length >= 3 ? args[2] : "nosha";
                SyntheticSweeps.Run(outDir, shaArg);
                return;
            }

            // Steady-state job: tiered compilation disabled so the dynamically-emitted
            // compiled delegate is fully optimized from the first timed invocation,
            // removing Tier-0→Tier-1 bimodality. invocationCount pinned so the in-memory
            // journal stays bounded (iterationCount × invocationCount entries).
            var job = Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithWarmupCount(8)
                .WithIterationCount(15)
                .WithInvocationCount(20000)
                .WithUnrollFactor(1)
                .WithEnvironmentVariable("DOTNET_TieredCompilation", "0")
                .WithId("SteadyNoTiering");

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job)
                .AddColumn(StatisticColumn.Mean)
                .AddColumn(StatisticColumn.Error)        // half of 99.9% CI
                .AddColumn(StatisticColumn.StdDev)
                .AddColumn(StatisticColumn.Median)
                .AddColumn(StatisticColumn.P95)
                .AddColumn(StatisticColumn.Min)
                .AddColumn(StatisticColumn.Max)
                .AddExporter(MarkdownExporter.GitHub)
                .AddExporter(CsvExporter.Default)
                .WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default
                    .WithTimeUnit(TimeUnit.Microsecond));

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
