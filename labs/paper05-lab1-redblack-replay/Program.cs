// Paper 5 — Lab 1: red-black replay time vs journal size (substrate equivalence E1: deployment = replay).
//
// Measures the red-black handover window as a function of journal size N, using ONLY the public
// Puppeteer surface — no internal instrumentation hooks. A leader is pre-populated with N compact
// events (bootstrap, outside the timed window); the leader is disposed; a follower spins up against
// the same FileSystem journal and the deploy window is timed:
//
//     Stopwatch → follower.Start(asFollower: true)        // bulk replay to near-head
//               → follower.LockWhileNotSyncronized()      // pause writes, read the handover tail
//               → follower.UnlockAndRunAlive()            // gate flip: follower becomes authoritative
//     Stopwatch stop; assert follower.IsAlive
//
// The replay rate is derived as N / bulk-replay-ms: exactly N events were written by the leader, so
// exactly N are replayed by the follower. This is the public-surface measurement — coarser than an
// in-runtime per-event counter, but it needs no framework modification, which is the point.
//
// Régime: compact-action journal under AlwaysCompiled (Paper 2). Set explicitly on both actors.
// Cross-check: after the follower is alive, PerformQuery reads _seq and asserts it equals N.
//
// Usage:  dotnet run -c Release            # full headline sweep: N in {1k,10k,100k}x3 + 1M anchor
//         dotnet run -c Release -- smoke   # smoke: N in {100,1000}x1

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Choreography.Theater;
using Puppeteer;

internal static class Program
{
    // The library assembly the actor scans for domain types. This lab has no domain types
    // (its verb is a pure DSL assignment), so any clean assembly serves; the Puppeteer runtime
    // assembly is a known-clean target.
    private static readonly Assembly LibAssembly = typeof(ActorV2).Assembly;

    private const string CompileModeLabel = "AlwaysCompiled";

    private sealed record HandoffSample(
        long N, int Repetition, double DeployTotalMs, double BulkReplayMs,
        double HandoverTailMs, long JournalBytes, bool CrossCheckOk);

    private static int Main(string[] args)
    {
        bool smoke = args.Length > 0 && args[0].Equals("smoke", StringComparison.OrdinalIgnoreCase);

        string gitSha = TryReadGitSha();
        string host = $"{Environment.OSVersion} / {Environment.ProcessorCount}cores / .NET {Environment.Version}";
        string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string labFolder = AppContext.BaseDirectory;
        string outDir = Path.Combine(labFolder, "results", $"run-{utc}-{gitSha}");
        Directory.CreateDirectory(outDir);

        var samples = new List<HandoffSample>();
        bool allOk = true;

        if (smoke)
        {
            foreach (long n in new long[] { 100, 1_000 })
                samples.Add(RunOnce(n, repetition: 0));
        }
        else
        {
            long[] ns = { 1_000, 10_000, 100_000 };
            RunOnce(ns[0], repetition: -1);   // warm-up (JIT + page cache), discarded
            foreach (long n in ns)
                for (int k = 0; k < 3; k++)
                    samples.Add(RunOnce(n, repetition: k));
            samples.Add(RunOnce(1_000_000, repetition: 0));   // 1M anchor, single repetition
        }

        WriteHandoffsCsv(samples, outDir, gitSha, host);
        WriteSummaryCsv(samples, outDir, gitSha, host);

        Console.WriteLine();
        Console.WriteLine($"=== Lab 1 — red-black replay ({(smoke ? "smoke" : "full")}) @ {gitSha} ===");
        foreach (var grp in samples.GroupBy(s => s.N).OrderBy(g => g.Key))
        {
            var bulk = grp.Select(s => s.BulkReplayMs).OrderBy(v => v).ToList();
            var rate = grp.Select(s => s.BulkReplayMs > 0 ? s.N / (s.BulkReplayMs / 1000.0) : 0).OrderBy(v => v).ToList();
            bool ok = grp.All(s => s.CrossCheckOk);
            allOk &= ok;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "N={0,9}: bulk_replay p50={1,8:F1}ms  rate p50={2,12:N0} ev/s  cross-check={3}",
                grp.Key, Percentile(bulk, 0.50), Percentile(rate, 0.50), ok ? "PASS" : "FAIL"));
        }
        Console.WriteLine($"Data: {outDir}");
        Console.WriteLine(allOk ? "ALL CROSS-CHECKS PASS" : "CROSS-CHECK FAILURE");
        return allOk ? 0 : 1;
    }

    private static HandoffSample RunOnce(long n, int repetition)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));

        string actorName = $"lab1_{n}_{repetition}_{Guid.NewGuid():N}".Substring(0, 24);
        string dataDir = Path.Combine(Path.GetTempPath(), "Lab1_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dataDir);
        try
        {
            BootstrapLeader(actorName, dataDir, n);
            long journalBytes = DirectorySize(dataDir);

            using var follower = new PerformanceV2(actorName, LibAssembly);
            follower.ConfigureStorage(DatabaseType.FileSystem, $"path={dataDir}");
            // CompiledModePolicy is not set here or below any more: both read AlwaysCompiled, and
            // the engine's Automatic default already compiles a V2 parametric command, so the lines
            // stated the regime rather than choosing it. Same regime measured; setter now internal.

            var sw = Stopwatch.StartNew();
            follower.Start(asFollower: true);
            double bulkMs = sw.Elapsed.TotalMilliseconds;
            if (follower.IsAlive) throw new InvalidOperationException("Follower alive before handover");

            follower.LockWhileNotSyncronized();
            follower.UnlockAndRunAlive();
            sw.Stop();
            double totalMs = sw.Elapsed.TotalMilliseconds;

            if (!follower.IsAlive) throw new InvalidOperationException("Follower not alive after handover");

            string q = follower.Actor.Using("{ print _seq 'value'; }").PerformQuery();
            bool ok = string.Equals(q, $"{{\"value\":{n}}}", StringComparison.Ordinal);

            if (repetition >= 0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  N={0,9} rep={1}: deploy={2,8:F1}ms bulk={3,8:F1}ms tail={4,6:F3}ms {5}",
                    n, repetition, totalMs, bulkMs, totalMs - bulkMs, ok ? "ok" : "MISMATCH"));

            return new HandoffSample(n, repetition, totalMs, bulkMs, totalMs - bulkMs, journalBytes, ok);
        }
        finally
        {
            try { Directory.Delete(dataDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static void BootstrapLeader(string actorName, string dataDir, long n)
    {
        using var leader = new PerformanceV2(actorName, LibAssembly);
        leader.ConfigureStorage(DatabaseType.FileSystem, $"path={dataDir}");
        leader.Start();

        // Pure-overwrite parametric verb: each call after the first writes ONE compact action-event
        // entry (the first writes a define + invocation). After N calls with val=1..N, _seq holds N.
        for (int i = 1; i <= n; i++)
        {
            int val = i;
            leader.Actor.Using("_seq = @val;")
                .WithParameters(p => { p["val", typeof(int)] = val; })
                .PerformCommand();
        }

        string final = leader.Actor.Using("{ print _seq 'value'; }").PerformQuery();
        if (!string.Equals(final, $"{{\"value\":{n}}}", StringComparison.Ordinal))
            throw new InvalidOperationException($"Leader bootstrap cross-check failed: got {final}");
    }

    private static long DirectorySize(string dir)
    {
        long total = 0;
        if (!Directory.Exists(dir)) return 0;
        foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            try { total += new FileInfo(f).Length; } catch { /* skip */ }
        return total;
    }

    private static void WriteHandoffsCsv(List<HandoffSample> samples, string outDir, string gitSha, string host)
    {
        var sb = new StringBuilder();
        sb.AppendLine("git_sha,host,N_events,repetition,compile_mode,deploy_total_ms,bulk_replay_ms,handover_tail_ms,replay_events_per_sec,journal_bytes,cross_check_ok");
        foreach (var s in samples)
        {
            double rate = s.BulkReplayMs > 0 ? s.N / (s.BulkReplayMs / 1000.0) : 0;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:F4},{6:F4},{7:F4},{8:F2},{9},{10}",
                gitSha, host, s.N, s.Repetition, CompileModeLabel,
                s.DeployTotalMs, s.BulkReplayMs, s.HandoverTailMs, rate, s.JournalBytes, s.CrossCheckOk ? 1 : 0));
        }
        File.WriteAllText(Path.Combine(outDir, "handoffs.csv"), sb.ToString());
    }

    private static void WriteSummaryCsv(List<HandoffSample> samples, string outDir, string gitSha, string host)
    {
        var sb = new StringBuilder();
        sb.AppendLine("git_sha,host,N_events,compile_mode,deploy_total_ms_p50,deploy_total_ms_p95,bulk_replay_ms_p50,bulk_replay_ms_p95,replay_events_per_sec_p50,replay_events_per_sec_p95,repetitions");
        foreach (var grp in samples.GroupBy(s => s.N).OrderBy(g => g.Key))
        {
            var deploy = grp.Select(s => s.DeployTotalMs).OrderBy(v => v).ToList();
            var bulk = grp.Select(s => s.BulkReplayMs).OrderBy(v => v).ToList();
            var rate = grp.Select(s => s.BulkReplayMs > 0 ? s.N / (s.BulkReplayMs / 1000.0) : 0).OrderBy(v => v).ToList();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:F4},{5:F4},{6:F4},{7:F4},{8:F2},{9:F2},{10}",
                gitSha, host, grp.Key, CompileModeLabel,
                Percentile(deploy, 0.50), Percentile(deploy, 0.95),
                Percentile(bulk, 0.50), Percentile(bulk, 0.95),
                Percentile(rate, 0.50), Percentile(rate, 0.95), grp.Count()));
        }
        File.WriteAllText(Path.Combine(outDir, "summary.csv"), sb.ToString());
    }

    private static double Percentile(List<double> sortedAsc, double q)
    {
        if (sortedAsc == null || sortedAsc.Count == 0) return 0;
        if (sortedAsc.Count == 1) return sortedAsc[0];
        double pos = q * (sortedAsc.Count - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sortedAsc[lo];
        return sortedAsc[lo] * (1 - (pos - lo)) + sortedAsc[hi] * (pos - lo);
    }

    private static string TryReadGitSha()
    {
        try
        {
            using var p = new Process();
            p.StartInfo.FileName = "git";
            p.StartInfo.Arguments = "rev-parse --short HEAD";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            string sha = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);
            if (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(sha)) return sha;
        }
        catch { /* provenance is recorded in the paper; this is a convenience tag */ }
        return "unknown";
    }
}
