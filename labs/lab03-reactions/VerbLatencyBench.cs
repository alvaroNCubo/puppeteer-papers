using BenchmarkDotNet.Attributes;
using Puppeteer;

namespace Lab03Reactions
{
    // Absolute latency of a PerformCommand on a hot, in-memory actor — the
    // "fast verbs by construction" magnitude of Paper 3 §5.2. The verb is a
    // minimal domain verb (ReactionLab.Ping), so this is a floor: it isolates the
    // fixed dispatch + persist overhead the actor pays per command, with no host
    // work to speak of. Compiled path, tiered compilation disabled (Program.cs).
    public class VerbLatencyBench
    {
        private ActorV2 _actor;
        private int _n;

        [GlobalSetup]
        public void Setup()
        {
            _actor = new ActorV2("bdn_l3_verb", typeof(ReactionLab).Assembly);
            _actor.ConfigureStorage(DatabaseType.IN_MEMORY, "InMemory");
            _actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            _actor.Using("p = ReactionLab();").PerformCommand();
            for (int i = 0; i < 200; i++) Invoke();
            _n = 0;
        }

        [Benchmark]
        public void Verb() => Invoke();

        private void Invoke()
        {
            int n = _n++;
            _actor.Using("p.Ping(n);").WithParameters(p => { p["n", typeof(int)] = n; }).PerformCommand();
        }
    }
}
