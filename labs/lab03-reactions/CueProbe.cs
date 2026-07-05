using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Puppeteer;
using Puppeteer.EventSourcing.Follower;

namespace Lab03Reactions
{
    // One-shot Cue measurements (Paper 3 §6.1 "sub-second", §3.6 / claim 12
    // "exactly once per match"). Two runs share the same actor/Reaction setup:
    //
    //   Run(iters)        — end-to-end latency from issuing the trigger command to
    //                       the Cue Reaction's Program.Emit body firing, on a hot
    //                       push loop, asserting exactly one fire per match.
    //   RunCatchup(iters) — catch-up + restart correctness: a match journaled BEFORE
    //                       the loop activates must be caught up and fired exactly
    //                       once, and re-activating on the same storage (a restart)
    //                       must not re-fire the already-processed event.
    public static class CueProbe
    {
        // The Reaction fires when a ReactionLab() constructor is journaled; a single
        // Seek matches that construction. Named (not "r"/"S") so the setup reads.
        private const string ReactionName = "OnReactionLabConstructed";
        private const string SeekName = "construct";
        private const string TriggerCommand = "ReactionLab();";

        // A hot actor carrying the single-Seek Cue Reaction. The Reaction's
        // Program.Emit body fires @probe.Fire() (block-wrapped: a query forbids
        // top-level create/call statements); the probe is injected as the @probe
        // parameter. Shared by both runs so the setup is written once.
        private static ActorV2 BuildCueActor(System.Reflection.Assembly domainAssembly)
        {
            var actor = new ActorV2("cue_l3_" + Guid.NewGuid().ToString("N"), domainAssembly);
            actor.ConfigureStorage(DatabaseType.IN_MEMORY, "InMemory");
            actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;

            var probe = new ReactionProbe();
            actor.Reactions
                .DefineReaction(ReactionName)
                .Cue().Company().WithSharedHydration()
                .Seek(SeekName)
                    .OnMatch("ReactionLab()")
                    .Program.Emit("{ @probe.Fire(); }");
            actor.Reactions[ReactionName].WithParameters(p => { p["probe", typeof(ReactionProbe)] = probe; });
            return actor;
        }

        // Start the Reaction's continuous push loop on a background thread.
        private static Task StartPushLoop(ActorV2 actor, CancellationToken token)
            => Task.Run(() => actor.Reactions.ExecuteReactions(
                new[] { ReactionName }, ReactionExecutionMode.Continuous, token));

        // ---- catch-up + restart correctness (no timing) --------------------

        // The matching command is journaled BEFORE the loop activates, so delivery
        // flows through the catch-up poll (the signal-preemptible replay path) rather
        // than the live push. Validates, across many fresh actors:
        //  (1) every pre-journaled match is caught up and fired exactly once, and
        //  (2) re-activating on the same actor/storage does NOT re-fire it.
        public static void RunCatchup(int iters)
        {
            var domainAssembly = typeof(ReactionLab).Assembly;
            int misses = 0;
            int doubleFires = 0;
            int restartRefires = 0;

            for (int i = 0; i < iters; i++)
            {
                ActorV2 actor = BuildCueActor(domainAssembly);
                ProbeState.ResetCount();

                // Journal the match BEFORE activation -> pure catch-up delivery.
                actor.Using(TriggerCommand).PerformCommand();

                var catchupRun = new CancellationTokenSource();
                Task catchupLoop = StartPushLoop(actor, catchupRun.Token);
                Thread.Sleep(400); // catch-up drains the pre-journaled event
                long firesAfterCatchup = Interlocked.Read(ref ProbeState.FireCount);
                if (firesAfterCatchup == 0) misses++;
                else if (firesAfterCatchup > 1) doubleFires++;

                catchupRun.Cancel();
                try { catchupLoop.Wait(1000); } catch { }

                // Restart: re-activate on the same actor/storage. The persisted
                // checkpoint must prevent re-firing the already-processed event.
                var restartRun = new CancellationTokenSource();
                Task restartLoop = StartPushLoop(actor, restartRun.Token);
                Thread.Sleep(400); // long enough that a (wrong) re-fire would surface
                long firesAfterRestart = Interlocked.Read(ref ProbeState.FireCount);
                if (firesAfterRestart > firesAfterCatchup) restartRefires++;

                restartRun.Cancel();
                actor.GracefulExit();
                try { restartLoop.Wait(1000); } catch { }
            }

            bool ok = misses == 0 && doubleFires == 0 && restartRefires == 0;
            Console.WriteLine();
            Console.WriteLine("# Cue catch-up + restart correctness (one-shot single-Seek), IN_MEMORY");
            Console.WriteLine($"actors (pre-journaled match)    : {iters}");
            Console.WriteLine($"catch-up misses (fired 0)       : {misses}    (expect 0)");
            Console.WriteLine($"catch-up double-fires (fired>1) : {doubleFires}    (expect 0)");
            Console.WriteLine($"re-fires across restart         : {restartRefires}    (expect 0)");
            Console.WriteLine($"RESULT                          : {(ok ? "PASS — catch-up delivery exactly-once, no re-fire across restart" : "FAIL")}");
        }

        // ---- end-to-end latency + exactly-once (fresh hot actor per sample) --

        public static void Run(int iters)
        {
            var domainAssembly = typeof(ReactionLab).Assembly;
            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            const int warmupSamples = 20;

            var latenciesMs = new List<double>(iters);
            int misses = 0;
            int doubleFires = 0;

            // The timed window is one trigger command -> the Reaction body firing
            // (actor/loop setup excluded). Per sample the Reaction must fire exactly
            // once per match; a fire delta != 1 flags a miss or a double-fire.
            for (int sample = 0; sample < iters + warmupSamples; sample++)
            {
                CancellationTokenSource cancellation = null;
                Task pushLoop = null;
                ActorV2 actor = null;
                try
                {
                    actor = BuildCueActor(domainAssembly);
                    cancellation = new CancellationTokenSource();
                    pushLoop = StartPushLoop(actor, cancellation.Token);
                    Thread.Sleep(40); // catch-up settles before the timed trigger

                    ProbeState.Reset();
                    long firesBefore = Interlocked.Read(ref ProbeState.FireCount);

                    long issuedTicks = Stopwatch.GetTimestamp();
                    actor.Using(TriggerCommand).PerformCommand();
                    bool fired = ProbeState.Wait(5000);
                    long fireTicks = Volatile.Read(ref ProbeState.LastFireTicks);

                    Thread.Sleep(20); // allow any (erroneous) second fire to surface
                    long fireDelta = Interlocked.Read(ref ProbeState.FireCount) - firesBefore;

                    if (sample < warmupSamples)
                        continue;
                    if (!fired || fireDelta == 0)
                    {
                        misses++;
                    }
                    else
                    {
                        latenciesMs.Add((fireTicks - issuedTicks) * ticksToMs);
                        if (fireDelta > 1) doubleFires++;
                    }
                }
                catch (Exception ex)
                {
                    if (sample >= warmupSamples) misses++;
                    Debug.WriteLine("sample exception: " + ex.Message);
                }
                finally
                {
                    try
                    {
                        cancellation?.Cancel();
                        actor?.GracefulExit();
                        pushLoop?.Wait(400);
                    }
                    catch { }
                }
            }

            latenciesMs.Sort();
            Console.WriteLine();
            Console.WriteLine("# Cue end-to-end latency (issue -> reaction fire), IN_MEMORY, fresh hot actor/sample");
            Console.WriteLine($"samples fired         : {latenciesMs.Count}  (misses: {misses})");
            Console.WriteLine($"exactly-once per match: {(doubleFires == 0 ? "held" : "VIOLATED")} (double-fires: {doubleFires}, of {latenciesMs.Count})");
            if (latenciesMs.Count > 0)
            {
                Console.WriteLine($"latency ms  min       : {latenciesMs[0]:F3}");
                Console.WriteLine($"latency ms  median    : {Percentile(latenciesMs, 0.50):F3}");
                Console.WriteLine($"latency ms  mean      : {Average(latenciesMs):F3}");
                Console.WriteLine($"latency ms  p95       : {Percentile(latenciesMs, 0.95):F3}");
                Console.WriteLine($"latency ms  p99       : {Percentile(latenciesMs, 0.99):F3}");
                Console.WriteLine($"latency ms  max       : {latenciesMs[latenciesMs.Count - 1]:F3}");
            }
        }

        // Percentile of an already-sorted list.
        private static double Percentile(List<double> sorted, double p)
            => sorted.Count == 0
                ? double.NaN
                : sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(p * sorted.Count))];

        private static double Average(List<double> values)
        {
            double sum = 0;
            foreach (double v in values) sum += v;
            return sum / values.Count;
        }
    }
}
