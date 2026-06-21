using System.Diagnostics;
using System.Threading;

namespace Lab03Reactions
{
    // Minimal domain facade for the Paper 3 Reaction measurements. `Ping` is a
    // representative small domain verb (one field write); the absolute latency of
    // dispatching it is the "fast verb" floor — richer verbs do more host work on
    // top of the same fixed dispatch/persist overhead.
    public class ReactionLab
    {
        public int Count;
        public ReactionLab Ping(int n) { Count = n; return this; }
        public ReactionLab Tick() { Count = Count + 1; return this; }
    }

    // The Cue Reaction's Program.Emit body calls `pr.Fire()` when its pattern
    // matches. Instance method so the DSL emit script can invoke it on a global
    // created at bootstrap; all observable state is static so the driver thread
    // can read it across the push-loop thread.
    public class ReactionProbe
    {
        public ReactionProbe Fire()
        {
            ProbeState.LastFireTicks = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref ProbeState.FireCount);
            ProbeState.Signal.Set();
            return this;
        }
    }

    public static class ProbeState
    {
        public static long FireCount;
        public static long LastFireTicks;
        public static readonly ManualResetEventSlim Signal = new ManualResetEventSlim(false);
        public static void Reset() => Signal.Reset();
        public static bool Wait(int ms) => Signal.Wait(ms);
        public static void ResetCount() => Interlocked.Exchange(ref FireCount, 0);
    }
}
