using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Puppeteer;

namespace BenchPaper2Bdn
{
    // Paper 2 Lab 1 (Release / BenchmarkDotNet replay) — compiled vs interpreted speedup
    // across the DSL-bound→domain-bound curve cited in §5.2 and the abstract:
    //   - Arith100 : straight-line arithmetic, depth 100 (DSL-bound end)        — synthetic, domain-independent
    //   - DslRich  : for + if + arithmetic, ~500 dispatched ops                  — synthetic, domain-independent
    //   - Production: eShop Order CompleteOrder verb (domain-bound end)          — public dotnet/eShop (MIT)
    //
    // Both Interpreted and Compiled walk the same AST; only the compiled path lowers it to
    // an Expression-tree delegate. Both journal identically, so the per-op ratio isolates the
    // DSL specialization. GlobalSetup creates one actor per mode, bootstraps, and warms the
    // compiled cache; invocationCount is pinned so the in-memory journal stays bounded
    // (iterationCount × invocationCount entries total).
    public enum Tier { Arith100, DslRich, Production }

    // Job (incl. tiered-compilation disable) is defined in Program.cs so the
    // steady-state, fully-optimized regime is measured without Tier-0→Tier-1
    // bimodality on the dynamically-emitted compiled delegate.
    public class Lab1SpeedupBench
    {
        [Params(Tier.Arith100, Tier.DslRich, Tier.Production)]
        public Tier Workload;

        private ActorV2 _compiled;
        private ActorV2 _interpreted;
        private string _script;
        private int[] _numbers;
        private int _n;

        private const string ProductionBootstrap = "f = OrderingFacade();";
        private const string ProductionScript = "o = f.CompleteOrder(uid, uname, pid, price, units);";

        [GlobalSetup]
        public void Setup()
        {
            _script = Workload switch
            {
                Tier.Arith100 => BuildArithmetic(100),
                Tier.DslRich => BuildRich(bodyDepth: 10),
                Tier.Production => ProductionScript,
                _ => throw new ArgumentOutOfRangeException()
            };
            _numbers = Enumerable.Range(0, 50).Select(i => i * 3).ToArray();

            var asm = typeof(OrderingFacade).Assembly;
            _interpreted = NewActor(CompilationModePolicy.AlwaysInterpreted, asm);
            _compiled = NewActor(CompilationModePolicy.AlwaysCompiled, asm);

            // Warm both: parser/parameter pools seeded; under AlwaysCompiled the program
            // is compiled once and cached, so timed invocations are all warm.
            for (int i = 0; i < 200; i++) { Invoke(_interpreted); Invoke(_compiled); }
            _n = 0;
        }

        private ActorV2 NewActor(CompilationModePolicy mode, System.Reflection.Assembly asm)
        {
            var actor = new ActorV2($"bdn_l1_{Workload}_{mode}", asm);
            actor.CompiledModePolicy = mode;
            if (Workload == Tier.Production)
                actor.Using(ProductionBootstrap).PerformCommand();
            return actor;
        }

        [Benchmark(Baseline = true)]
        public void Interpreted() => Invoke(_interpreted);

        [Benchmark]
        public void Compiled() => Invoke(_compiled);

        private void Invoke(ActorV2 actor)
        {
            int n = _n++;
            switch (Workload)
            {
                case Tier.Arith100:
                    actor.Using(_script).WithParameters(p => { p["X", typeof(int)] = n; }).PerformCommand();
                    break;
                case Tier.DslRich:
                    actor.Using(_script).WithParameters(p =>
                    {
                        p["Numbers", typeof(int[])] = _numbers;
                        p["Threshold", typeof(int)] = n % 5;
                    }).PerformCommand();
                    break;
                case Tier.Production:
                    actor.Using(_script).WithParameters(p =>
                    {
                        p["uid", typeof(string)] = "user-1";
                        p["uname", typeof(string)] = "Bench User";
                        p["pid", typeof(int)] = n;
                        p["price", typeof(decimal)] = 10.0m;
                        p["units", typeof(int)] = 1;
                    }).PerformCommand();
                    break;
            }
        }

        // Straight-line arithmetic: depth dependent statements, one int parameter X.
        private static string BuildArithmetic(int depth)
        {
            var sb = new StringBuilder();
            sb.Append("{ v0 = X + 1; ");
            for (int i = 1; i < depth; i++)
                sb.Append($"v{i} = v{i - 1} {(i % 2 == 0 ? "+" : "-")} 1; ");
            sb.Append($"print v{depth - 1} 'value'; }}");
            return sb.ToString();
        }

        // for-each over an int[] with if/else and a chain of bodyDepth arithmetic statements
        // in the taken branch. ~bodyDepth × |Numbers| dispatched ops per invocation.
        private static string BuildRich(int bodyDepth)
        {
            var sb = new StringBuilder();
            sb.Append("{ total = 0; for (n : Numbers) { if (n > Threshold) { a0 = n + 1; ");
            for (int i = 1; i < bodyDepth; i++)
                sb.Append($"a{i} = a{i - 1} {(i % 2 == 0 ? "+" : "-")} 1; ");
            sb.Append($"total = total + a{bodyDepth - 1}; }} else {{ total = total - 1; }} }} print total 'value'; }}");
            return sb.ToString();
        }
    }
}
