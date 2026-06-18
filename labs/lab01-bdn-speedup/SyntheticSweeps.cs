using System.Diagnostics;
using System.Text;
using Puppeteer;

namespace BenchPaper2Bdn
{
    // §5.2 synthetic compile-cost-by-depth and §5.3 synthetic eval-complexity sweeps.
    // These are one-shot / cold costs (program compile, eval sub-program recompile)
    // isolated by the public LabInstrumentation hooks — Stopwatch is the appropriate
    // instrument for hundreds-of-µs single events; BenchmarkDotNet is reserved for the
    // steady-state throughput speedup (Lab 1). Domain-independent: bootstrapped on the
    // eShop assembly only because ActorV2 requires a library assembly.
    public static class SyntheticSweeps
    {
        private static double Us(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
        private static double P(List<double> xs, double p)
        {
            var s = xs.OrderBy(x => x).ToList();
            return s[Math.Min(s.Count - 1, (int)(s.Count * p))];
        }

        public static void Run(string outDir, string sha)
        {
            Directory.CreateDirectory(outDir);
            CompileCostByDepth(outDir, sha);
            EvalComplexity(outDir, sha);
        }

        // §5.2 — cost of programExpression.Compile() as a function of statement count.
        private static void CompileCostByDepth(string outDir, string sha)
        {
            int[] depths = { 5, 15, 30, 50, 80, 100 };
            const int VariantsPerDepth = 50;
            var asm = typeof(OrderingFacade).Assembly;

            var rows = new List<(int depth, double us)>();
            Console.WriteLine("=== §5.2 synthetic — compile cost by statement count (cold, isolated) ===");
            Console.WriteLine($"{"depth",6} {"p50_us",10} {"p95_us",10} {"mean_us",10}");

            foreach (int depth in depths)
            {
                var actor = new ActorV2($"syn_compile_d{depth}", asm);
                actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;

                var ticks = new List<long>(VariantsPerDepth);
                LabInstrumentation.OnCompileElapsedTicks = t => ticks.Add(t);
                try
                {
                    for (int i = 0; i < VariantsPerDepth; i++)
                        actor.Using(BuildArithmeticVariant(depth, i))
                             .WithParameters(p => { p["X", typeof(int)] = i; })
                             .PerformCommand();
                }
                finally { LabInstrumentation.OnCompileElapsedTicks = null; }

                var us = ticks.Select(Us).ToList();
                double p50 = P(us, 0.50), p95 = P(us, 0.95), mean = us.Average();
                Console.WriteLine($"{depth,6} {p50,10:F1} {p95,10:F1} {mean,10:F1}");
                foreach (var u in us) rows.Add((depth, u));
            }

            // Per-statement slope from p50 endpoints.
            var byDepth = rows.GroupBy(r => r.depth).ToDictionary(g => g.Key, g => P(g.Select(x => x.us).ToList(), 0.50));
            double slope = (byDepth[100] - byDepth[5]) / (100 - 5);
            Console.WriteLine($"Per-statement slope (p50, depth 5→100): {slope:F2} µs/statement");

            var path = Path.Combine(outDir, $"compile-by-depth-{Stamp()}-{sha}.csv");
            using var w = new StreamWriter(path);
            w.WriteLine("depth,compile_us");
            foreach (var (d, u) in rows) w.WriteLine($"{d},{u:F3}");
            Console.WriteLine($"CSV: {path}\n");
        }

        // §5.3 — eval sub-program cache hit vs miss across sub-program complexities.
        private static void EvalComplexity(string outDir, string sha)
        {
            var complexities = new (string name, int terms)[] { ("2-term", 2), ("50-term", 50) };
            const int N = 1000;
            var asm = typeof(OrderingFacade).Assembly;
            const string OuterScript = "{ y = (int)(evalParam + @counter); print y 'v'; }";

            var rows = new List<(string complexity, string regime, double endUs, double compUs)>();
            Console.WriteLine("=== §5.3 synthetic — eval cache hit vs miss by sub-program complexity ===");
            Console.WriteLine($"{"complexity",12} {"hit_p50_us",12} {"miss_p50_us",12} {"ratio",8}");

            foreach (var (name, terms) in complexities)
            {
                string baseEval = BuildEvalExpr(terms);
                var hit = new List<double>();
                var miss = new List<double>();
                var missComp = new List<double>();

                foreach (var regime in new[] { "stable", "mutating" })
                {
                    var actor = new ActorV2($"syn_eval_{name}_{regime}", asm);
                    actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
                    // Warmup: compile outer + first eval under the base text.
                    actor.Using(OuterScript).WithParameters(p =>
                    {
                        p[Parameter.Eval, "evalParam", typeof(int)] = baseEval;
                        p[Parameter.In, "counter", typeof(int)] = 0;
                    }).PerformCommand();

                    for (int i = 0; i < N; i++)
                    {
                        string evalText = regime == "stable" ? baseEval : baseEval + " + " + i;
                        long compTicks = -1;
                        LabInstrumentation.OnEvalCompileElapsedTicks = t => compTicks = t;
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            actor.Using(OuterScript).WithParameters(p =>
                            {
                                p[Parameter.Eval, "evalParam", typeof(int)] = evalText;
                                p[Parameter.In, "counter", typeof(int)] = i;
                            }).PerformCommand();
                        }
                        finally { LabInstrumentation.OnEvalCompileElapsedTicks = null; }
                        sw.Stop();
                        double endUs = Us(sw.ElapsedTicks);
                        if (regime == "stable") hit.Add(endUs);
                        else { miss.Add(endUs); if (compTicks >= 0) missComp.Add(Us(compTicks)); }
                        rows.Add((name, regime, endUs, compTicks >= 0 ? Us(compTicks) : -1));
                    }
                }

                double hitP50 = P(hit, 0.50), missP50 = P(miss, 0.50);
                Console.WriteLine($"{name,12} {hitP50,12:F2} {missP50,12:F2} {missP50 / hitP50,8:F1}");
            }

            var path = Path.Combine(outDir, $"eval-complexity-{Stamp()}-{sha}.csv");
            using var w = new StreamWriter(path);
            w.WriteLine("complexity,regime,end_to_end_us,eval_compile_us");
            foreach (var (c, r, e, cu) in rows) w.WriteLine($"{c},{r},{e:F3},{(cu >= 0 ? cu.ToString("F3") : "")}");
            Console.WriteLine($"CSV: {path}\n");
        }

        private static string BuildArithmeticVariant(int depth, int seq)
        {
            var sb = new StringBuilder();
            sb.Append($"_seq_{seq} = {seq}; {{ v0 = X + 1; ");
            for (int i = 1; i < depth; i++)
                sb.Append($"v{i} = v{i - 1} {(i % 2 == 0 ? "+" : "-")} 1; ");
            sb.Append($"print v{depth - 1} 'value'; }}");
            return sb.ToString();
        }

        private static string BuildEvalExpr(int terms)
        {
            var sb = new StringBuilder("@counter");
            for (int i = 1; i < terms; i++) sb.Append(" + 1");
            return sb.ToString();
        }

        private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
    }
}
