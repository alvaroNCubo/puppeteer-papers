using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Puppeteer;
using Puppeteer.EventSourcing.Follower;

namespace Lab03Reactions
{
    // One-shot Cue measurements (Paper 3 §6.1 "sub-second", §3.6/§claim 12
    // "exactly once per match"). Measures end-to-end latency from issuing the
    // command to the Cue Reaction's Program.Emit body firing, on a hot push loop,
    // and asserts the Reaction fires exactly once per matching command.
    public static class CueProbe
    {
        // Build a hot actor with a single-Seek Cue Reaction that fires `@probe.Fire()`
        // (block-wrapped: a query forbids top-level create/call statements) when a
        // ReactionLab() constructor is journaled. The push loop runs on a background
        // thread; the probe is injected as a parameter so the emit reads it as @probe.
        private static ActorV2 NewHotActor(System.Reflection.Assembly asm, out CancellationTokenSource cts, out Task loop)
        {
            var actor = new ActorV2("cue_l3_" + System.Guid.NewGuid().ToString("N"), asm);
            actor.ConfigureStorage(DatabaseType.IN_MEMORY, "InMemory");
            actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            var probe = new ReactionProbe();
            actor.Reactions
                .DefineReaction("r")
                .Cue().Company().WithSharedHydration()
                .Seek("S")
                    .OnMatch("ReactionLab()")
                    .Program.Emit("{ @probe.Fire(); }");
            actor.Reactions["r"].WithParameters(pp => { pp["probe", typeof(ReactionProbe)] = probe; });
            var c = cts = new CancellationTokenSource();
            var a = actor;
            loop = Task.Run(() => a.Reactions.ExecuteReactions(new[] { "r" }, ReactionExecutionMode.Continuous, c.Token));
            return actor;
        }

        // Catch-up + restart correctness for the one-shot single-Seek Cue (no
        // timing). The matching command is journaled BEFORE the loop activates,
        // so delivery flows entirely through the catch-up poll (CanContinueReplay,
        // the path made signal-preemptible) rather than the live push. Validates:
        //  (1) every pre-journaled match is caught up and fired exactly once
        //      (no miss, no double-fire) across many fresh actors, and
        //  (2) re-activating on the same actor/storage (a restart) does NOT
        //      re-fire the already-processed event (exactly-once across restart).
        public static void RunCatchup(int iters)
        {
            var asm = typeof(ReactionLab).Assembly;
            int misses = 0, doubles = 0, restartRefires = 0;

            for (int i = 0; i < iters; i++)
            {
                var actor = new ActorV2("cue_catchup_" + System.Guid.NewGuid().ToString("N"), asm);
                actor.ConfigureStorage(DatabaseType.IN_MEMORY, "InMemory");
                actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
                var probe = new ReactionProbe();
                actor.Reactions
                    .DefineReaction("r")
                    .Cue().Company().WithSharedHydration()
                    .Seek("S")
                        .OnMatch("ReactionLab()")
                        .Program.Emit("{ @probe.Fire(); }");
                actor.Reactions["r"].WithParameters(pp => { pp["probe", typeof(ReactionProbe)] = probe; });

                ProbeState.ResetCount();

                // Journal the match BEFORE activation -> pure catch-up delivery.
                actor.Using("ReactionLab();").PerformCommand();

                var cts1 = new CancellationTokenSource();
                var loop1 = Task.Run(() => actor.Reactions.ExecuteReactions(new[] { "r" }, ReactionExecutionMode.Continuous, cts1.Token));
                Thread.Sleep(400); // catch-up drains the pre-journaled event
                long afterCatchup = Interlocked.Read(ref ProbeState.FireCount);
                if (afterCatchup == 0) misses++;
                else if (afterCatchup > 1) doubles++;

                // Restart: cancel, re-activate on the same actor/storage. The
                // persisted checkpoint must prevent re-firing the processed event.
                cts1.Cancel(); try { loop1.Wait(1000); } catch { }
                var cts2 = new CancellationTokenSource();
                var loop2 = Task.Run(() => actor.Reactions.ExecuteReactions(new[] { "r" }, ReactionExecutionMode.Continuous, cts2.Token));
                Thread.Sleep(400); // long enough that a (wrong) re-fire would be observed
                long afterRestart = Interlocked.Read(ref ProbeState.FireCount);
                if (afterRestart > afterCatchup) restartRefires++;

                cts2.Cancel(); actor.GracefulExit(); try { loop2.Wait(1000); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine("# Cue catch-up + restart correctness (one-shot single-Seek), IN_MEMORY");
            Console.WriteLine($"actors (pre-journaled match)    : {iters}");
            Console.WriteLine($"catch-up misses (fired 0)       : {misses}    (expect 0)");
            Console.WriteLine($"catch-up double-fires (fired>1) : {doubles}    (expect 0)");
            Console.WriteLine($"re-fires across restart         : {restartRefires}    (expect 0)");
            bool ok = misses == 0 && doubles == 0 && restartRefires == 0;
            Console.WriteLine($"RESULT                          : {(ok ? "PASS — catch-up delivery exactly-once, no re-fire across restart" : "FAIL")}");
        }

        public static void Run(int iters)
        {
            var asm = typeof(ReactionLab).Assembly;
            double tickToMs = 1000.0 / Stopwatch.Frequency;

            // --- latency + exactly-once: fresh hot actor per sample. The timed window
            //     is one trigger command -> Reaction body firing (actor/loop setup
            //     excluded). Per sample the Reaction must fire exactly once per match;
            //     fireDelta != 1 would flag a miss or a double-fire. ---
            const int warm = 20;
            var lat = new System.Collections.Generic.List<double>(iters);
            int misses = 0, doubles = 0;
            for (int i = 0; i < iters + warm; i++)
            {
                CancellationTokenSource cts = null; Task loop = null; ActorV2 a = null;
                try
                {
                    a = NewHotActor(asm, out cts, out loop);
                    Thread.Sleep(40); // catch-up settles before the timed trigger
                    ProbeState.Reset();
                    long before = Interlocked.Read(ref ProbeState.FireCount);
                    long t0 = Stopwatch.GetTimestamp();
                    a.Using("ReactionLab();").PerformCommand();
                    bool got = ProbeState.Wait(5000);
                    long tf = Volatile.Read(ref ProbeState.LastFireTicks);
                    Thread.Sleep(20); // allow any (erroneous) second fire to surface
                    long delta = Interlocked.Read(ref ProbeState.FireCount) - before;
                    if (i >= warm)
                    {
                        if (!got || delta == 0) misses++;
                        else { lat.Add((tf - t0) * tickToMs); if (delta > 1) doubles++; }
                    }
                }
                catch (Exception ex) { if (i >= warm) misses++; System.Diagnostics.Debug.WriteLine("sample ex: " + ex.Message); }
                finally { try { cts?.Cancel(); a?.GracefulExit(); loop?.Wait(400); } catch { } }
            }

            lat.Sort();
            double Pct(double p) => lat.Count == 0 ? double.NaN
                : lat[(int)Math.Min(lat.Count - 1, Math.Floor(p * lat.Count))];
            Console.WriteLine();
            Console.WriteLine("# Cue end-to-end latency (issue -> reaction fire), IN_MEMORY, fresh hot actor/sample");
            Console.WriteLine($"samples fired         : {lat.Count}  (misses: {misses})");
            Console.WriteLine($"exactly-once per match: {(doubles == 0 ? "held" : "VIOLATED")} (double-fires: {doubles}, of {lat.Count})");
            if (lat.Count > 0)
            {
                Console.WriteLine($"latency ms  min       : {lat[0]:F3}");
                Console.WriteLine($"latency ms  median    : {Pct(0.50):F3}");
                Console.WriteLine($"latency ms  mean      : {lat.Average():F3}");
                Console.WriteLine($"latency ms  p95       : {Pct(0.95):F3}");
                Console.WriteLine($"latency ms  p99       : {Pct(0.99):F3}");
                Console.WriteLine($"latency ms  max       : {lat[lat.Count - 1]:F3}");
            }
        }
    }
}
