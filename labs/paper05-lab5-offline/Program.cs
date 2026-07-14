// Paper 5 — Lab 5: offline operation (claim 5 / E4 — the local buffer + async flush).
//
// An actor configured with a LOCAL buffered journal (`localBufferPath=…`) appends to a
// fast local WAL and replicates asynchronously to a remote canonical backend (MySQL /
// SQL Edge). The lab:
//   1. Online baseline — N appends; compares direct (straight-to-remote) vs buffered
//      per-append latency (the buffer speedup).
//   2. Partition — docker-stops the remote, keeps appending M events to the local
//      buffer (latency must be UNCHANGED), backlog grows.
//   3. Catch-up — docker-starts the remote, drains the backlog, measures the drain rate
//      and confirms ZERO events lost (a replica over the remote reaches the primary's
//      state).
//
// APPENDS = PUBLIC. Every append is a public PerformCommand of the compact verb
// `_seq = @val;` under AlwaysCompiled; the buffered-vs-direct path is selected purely by
// the presence of the `localBufferPath=` connection-string key (public). Stopwatch.
// GetTimestamp() brackets each append.
//
// OBSERVERS = documented internal grant. The buffered-vs-direct progress observers are
// purpose-built on Diary for this lab (`// paper05-lab5: harness-facing observers`,
// Diary.cs:34-41) but are internal: LastReplicatedEntryId / PendingReplicationCount /
// LocalBufferLastWrittenEntryId. Read via actor.Handler.TryGetDiary() under
// InternalsVisibleTo("Lab05L5Offline"). The zero-loss check itself is PUBLIC (a replica
// PerformQuery over the remote).
//
// Usage:  dotnet run -c Release -- <outDir> <runTag> [smoke]
//         env: LAB5_BACKENDS=MySQL,SQLServer  LAB5_N=10000  LAB5_M=10000  LAB5_REPS=2

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

internal static class Program
{
    private static readonly Assembly LibAssembly = typeof(ActorV2).Assembly;

    private static readonly string[] DefaultBackends = { "MySQL", "SQLServer" };
    private const int DefaultN = 10_000;
    private const int DefaultM = 10_000;
    private const int DefaultReps = 2;
    private const int WarmupN = 1_000;
    private const int CatchupPollMs = 100;
    private const int CatchupTimeoutMs = 300_000;
    private const int HealthTimeoutMs = 120_000;

    // Ports 3307 / 1434 (avoid L4/L6 collisions). Short connection timeout so partition
    // failures surface fast; pooling so the pool refreshes stale connections after stop/start.
    private const string MySqlConn =
        "server=localhost;port=3307;database=lab5_mysql;user id=root;password=puppeteer;SslMode=none;DefaultCommandTimeout=60;Connection Timeout=5;Pooling=true;Min Pool Size=0";
    private const string MsSqlConn =
        "server=localhost,1434;database=lab5_mssql;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false;Connection Timeout=5;Pooling=true;Min Pool Size=0";
    private const string MsSqlMasterConn =
        "server=localhost,1434;database=master;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false;Connection Timeout=10";
    private const string MySqlContainer = "paper05_lab5_mysql";
    private const string MsSqlContainer = "paper05_lab5_mssql";

    private enum Backend { MySQL, SQLServer }

    private static string runTag;
    private static int N, M, reps;
    private static StreamWriter samples;
    private static readonly List<Agg> aggregates = new();
    private static readonly List<Catchup> catchups = new();

    private sealed record Agg(string Cell, string Backend, string Mode, string Phase, long Samples,
        double MeanUs, double P50Us, double P95Us, double P99Us, double MaxUs, double EventsPerSec);
    private sealed record Catchup(string Cell, string Backend, long Backlog, double DrainSec, double EventsPerSec,
        int PrimaryValue, int ReplicaValue, bool ZeroLoss);

    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "results");
        runTag = args.Length > 1 ? args[1] : "unknown";
        bool smoke = args.Any(a => a.Equals("smoke", StringComparison.OrdinalIgnoreCase));

        N = EnvInt("LAB5_N", smoke ? 2_000 : DefaultN);
        M = EnvInt("LAB5_M", smoke ? 2_000 : DefaultM);
        reps = EnvInt("LAB5_REPS", smoke ? 1 : DefaultReps);
        string[] selected = EnvList("LAB5_BACKENDS", DefaultBackends);

        string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string host = $"{Environment.OSVersion} / {Environment.ProcessorCount}cores / .NET {Environment.Version}";
        string runDir = Path.Combine(outDir, $"run-{utc}-{runTag}");
        Directory.CreateDirectory(runDir);

        Console.WriteLine($"[Lab5] backends=[{string.Join(",", selected)}] N={N} M={M} reps={reps} runTag={runTag}");
        Console.WriteLine($"[Lab5] out={runDir}");

        samples = new StreamWriter(Path.Combine(runDir, "samples.csv"));
        samples.WriteLine("run_tag,cell,backend,mode,phase,rep,event_idx,append_micros");

        foreach (string backendName in selected)
        {
            Backend backend = Enum.Parse<Backend>(backendName, ignoreCase: true);
            if (!Probe(backend, out string skip))
            {
                Console.WriteLine($"[Lab5] SKIP {backendName}: {skip}");
                continue;
            }
            foreach (bool buffered in new[] { false, true })
            {
                string cell = $"{backendName}_{(buffered ? "buffered" : "direct")}";
                Console.WriteLine($"[Lab5] === cell {cell} ===");
                var online = new List<long>(N * reps);
                var partition = new List<long>(M * reps);
                for (int rep = 0; rep < reps; rep++)
                {
                    var r = RunCell(backend, backendName, buffered, cell, rep);
                    online.AddRange(r.online);
                    if (r.partition != null) partition.AddRange(r.partition);
                    if (r.catchup != null) catchups.Add(r.catchup);
                }
                aggregates.Add(Aggregate(cell, backendName, buffered ? "buffered" : "direct", "Online", online));
                if (partition.Count > 0)
                    aggregates.Add(Aggregate(cell, backendName, "buffered", "Partition", partition));
            }
        }

        samples.Flush(); samples.Dispose();
        WriteSummary(runDir);
        WriteHeadline(runDir, host, utc);
        Console.WriteLine($"[Lab5] dataset written to {runDir}");
        return 0;
    }

    private sealed record CellResult(long[] online, long[] partition, Catchup catchup);

    private static CellResult RunCell(Backend backend, string backendName, bool buffered, string cell, int rep)
    {
        string actorName = $"lab5_{backendName}_{(buffered ? "b" : "d")}_{rep}_{Guid.NewGuid().ToString("N")[..8]}";
        string bufferPath = null;
        string container = backend == Backend.MySQL ? MySqlContainer : MsSqlContainer;
        DatabaseType db = backend == Backend.MySQL ? DatabaseType.MySQL : DatabaseType.SQLServer;
        string conn = backend == Backend.MySQL ? MySqlConn : MsSqlConn;
        if (buffered)
        {
            bufferPath = Path.Combine(Path.GetTempPath(), "Lab5Buf_" + Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(bufferPath);
            conn = conn + $";localBufferPath={bufferPath}";
        }

        PerformanceV2 perf = null;
        int lastValue = 0;
        try
        {
            perf = new PerformanceV2(actorName, LibAssembly);
            perf.ConfigureStorage(db, conn);
            perf.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            perf.Start();
            var diary = buffered ? perf.Actor.Handler.TryGetDiary() : null;

            // Warm-up (schema creation, JIT, first compact-action shape) — discarded.
            for (int i = 0; i < WarmupN; i++) { int v = i; Append(perf, v); lastValue = v; }

            // ─── Online baseline ───
            long[] online = new long[N];
            for (int i = 0; i < N; i++)
            {
                int v = WarmupN + i;
                long t0 = Stopwatch.GetTimestamp();
                Append(perf, v);
                online[i] = Stopwatch.GetTimestamp() - t0;
                lastValue = v;
            }
            EmitSamples(cell, backendName, buffered ? "buffered" : "direct", "Online", rep, online);
            Console.WriteLine($"[Lab5]   {cell} rep {rep} online p50={Micros(Median(online)):F1}us");

            long[] partition = null;
            Catchup catchup = null;

            if (buffered)
            {
                WaitBacklogDrained(diary, 30_000);

                // ─── Partition ───
                Console.WriteLine($"[Lab5]   {cell} rep {rep}: docker stop {container}");
                Docker($"stop --time 10 {container}");
                partition = new long[M];
                for (int i = 0; i < M; i++)
                {
                    int v = WarmupN + N + i;
                    long t0 = Stopwatch.GetTimestamp();
                    Append(perf, v);
                    partition[i] = Stopwatch.GetTimestamp() - t0;
                    lastValue = v;
                }
                EmitSamples(cell, backendName, "buffered", "Partition", rep, partition);
                long target = diary.LocalBufferLastWrittenEntryId;
                long startRepl = diary.LastReplicatedEntryId;
                long backlog = target - startRepl;
                Console.WriteLine($"[Lab5]   {cell} rep {rep} partition p50={Micros(Median(partition)):F1}us backlog={backlog}");

                // ─── Catch-up (full time-to-fully-replicated incl. container restart) ───
                long t0c = Stopwatch.GetTimestamp();
                Docker($"start {container}");
                WaitHealthy(container, HealthTimeoutMs);
                Reprobe(backend);
                bool drained = WaitCatchup(diary, target, CatchupTimeoutMs);
                double sec = (Stopwatch.GetTimestamp() - t0c) / (double)Stopwatch.Frequency;
                double eps = sec > 0 ? backlog / sec : double.NaN;

                // Zero-loss check (PUBLIC): a fresh replica over the REMOTE backend must
                // reach the primary's last value.
                int replicaValue = ReadReplicaValue(actorName, db, backend);
                bool zeroLoss = drained && replicaValue == lastValue;
                Console.WriteLine($"[Lab5]   {cell} rep {rep} catchup: drained={drained} {backlog} ev in {sec:F1}s ({eps:F0} ev/s) primary={lastValue} replica={replicaValue} zeroLoss={zeroLoss}");
                catchup = new Catchup(cell, backendName, backlog, sec, eps, lastValue, replicaValue, zeroLoss);
            }

            return new CellResult(online, partition, catchup);
        }
        finally
        {
            try { perf?.Actor.GracefulExit(); } catch { }
            try { perf?.Dispose(); } catch { }
            ForceGc();
            // Make sure the container is running for the next cell.
            Docker($"start {container}");
            TryDelete(bufferPath);
        }
    }

    private static void Append(PerformanceV2 perf, int v)
        => perf.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = v; }).PerformCommand();

    private static int ReadReplicaValue(string actorName, DatabaseType db, Backend backend)
    {
        try
        {
            WaitHealthy(backend == Backend.MySQL ? MySqlContainer : MsSqlContainer, 30_000);
            Reprobe(backend);
            using var replica = new PerformanceV2(actorName, LibAssembly);
            replica.ConfigureStorage(db, backend == Backend.MySQL ? MySqlConn : MsSqlConn);
            replica.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            replica.Start();
            string q = replica.Actor.Using("{ print _seq 'value'; }").PerformQuery();
            replica.Actor.GracefulExit();
            int idx = q.IndexOf(':');
            if (idx >= 0 && int.TryParse(q[(idx + 1)..].TrimEnd('}'), out int val)) return val;
        }
        catch (Exception ex) { Console.WriteLine($"[Lab5]   replica read failed: {ex.Message}"); }
        return -1;
    }

    private static void WaitBacklogDrained(Diary diary, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (diary.LastReplicatedEntryId >= diary.LocalBufferLastWrittenEntryId && diary.PendingReplicationCount == 0)
                return;
            Thread.Sleep(50);
        }
        Console.WriteLine($"[Lab5]   backlog drain wait timed out (repl={diary.LastReplicatedEntryId} buf={diary.LocalBufferLastWrittenEntryId} pending={diary.PendingReplicationCount})");
    }

    private static bool WaitCatchup(Diary diary, long target, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        long lastLog = Environment.TickCount64;
        while (Environment.TickCount64 < deadline)
        {
            if (diary.LastReplicatedEntryId >= target && diary.PendingReplicationCount == 0) return true;
            if (Environment.TickCount64 - lastLog > 5_000)
            {
                Console.WriteLine($"[Lab5]     catchup repl={diary.LastReplicatedEntryId}/{target} pending={diary.PendingReplicationCount} failures={diary.ReplicationFailureCount}");
                lastLog = Environment.TickCount64;
            }
            Thread.Sleep(CatchupPollMs);
        }
        return false;
    }

    // ─── Docker orchestration ───
    private static void Docker(string args)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo("docker", args)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            p.Start();
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit();
        }
        catch (Exception ex) { Console.WriteLine($"[Lab5]   docker {args} failed: {ex.Message}"); }
    }

    private static void WaitHealthy(string container, int timeoutMs)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            string status = DockerCapture($"inspect --format={{{{.State.Health.Status}}}} {container}").Trim();
            if (string.Equals(status, "healthy", StringComparison.OrdinalIgnoreCase)) return;
            Thread.Sleep(500);
        }
        throw new TimeoutException($"Container {container} not healthy within {timeoutMs} ms");
    }

    private static string DockerCapture(string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo("docker", args)
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        p.Start();
        string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
        p.WaitForExit();
        return o;
    }

    private static void Reprobe(Backend backend)
    {
        Exception last = null;
        for (int i = 0; i < 60; i++)
        {
            try
            {
                if (backend == Backend.MySQL)
                {
                    using var c = new MySql.Data.MySqlClient.MySqlConnection(MySqlConn);
                    c.Open();
                    using var cmd = new MySql.Data.MySqlClient.MySqlCommand("SELECT 1", c); cmd.ExecuteScalar();
                }
                else
                {
                    using var c = new Microsoft.Data.SqlClient.SqlConnection(MsSqlConn);
                    c.Open();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", c); cmd.ExecuteScalar();
                }
                return;
            }
            catch (Exception ex) { last = ex; Thread.Sleep(500); }
        }
        throw new TimeoutException($"Reprobe failed for {backend}: {last?.Message}");
    }

    private static bool Probe(Backend backend, out string skip)
    {
        skip = null;
        try
        {
            WaitHealthy(backend == Backend.MySQL ? MySqlContainer : MsSqlContainer, HealthTimeoutMs);
            if (backend == Backend.MySQL)
            {
                var b = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(MySqlConn) { Database = "" };
                using var server = new MySql.Data.MySqlClient.MySqlConnection(b.ConnectionString);
                server.Open();
                using var cmd = new MySql.Data.MySqlClient.MySqlCommand("CREATE DATABASE IF NOT EXISTS lab5_mysql;", server);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var master = new Microsoft.Data.SqlClient.SqlConnection(MsSqlMasterConn);
                master.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand("IF DB_ID('lab5_mssql') IS NULL CREATE DATABASE lab5_mssql;", master);
                cmd.ExecuteNonQuery();
            }
            Reprobe(backend);
            return true;
        }
        catch (Exception ex)
        {
            skip = ex.GetType().Name + ": " + ex.Message.Replace('\n', ' ').Replace('\r', ' ');
            if (skip.Length > 200) skip = skip[..200];
            return false;
        }
    }

    private static void EmitSamples(string cell, string backend, string mode, string phase, int rep, long[] ticks)
    {
        double freq = Stopwatch.Frequency;
        for (int i = 0; i < ticks.Length; i++)
            samples.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{cell},{backend},{mode},{phase},{rep},{i},{ticks[i] * 1_000_000.0 / freq:F3}"));
    }

    private static Agg Aggregate(string cell, string backend, string mode, string phase, List<long> ticks)
    {
        long[] s = ticks.ToArray(); Array.Sort(s);
        double meanTicks = s.Average();
        double eps = meanTicks > 0 ? Stopwatch.Frequency / meanTicks : double.NaN;
        return new Agg(cell, backend, mode, phase, s.LongLength, Micros(meanTicks),
            Micros(Pct(s, 0.50)), Micros(Pct(s, 0.95)), Micros(Pct(s, 0.99)), Micros(s[^1]), eps);
    }

    private static void WriteSummary(string runDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("run_tag,cell,backend,mode,phase,samples,mean_us,p50_us,p95_us,p99_us,max_us,events_per_sec");
        foreach (var a in aggregates)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{a.Cell},{a.Backend},{a.Mode},{a.Phase},{a.Samples},{a.MeanUs:F2},{a.P50Us:F2},{a.P95Us:F2},{a.P99Us:F2},{a.MaxUs:F2},{a.EventsPerSec:F0}"));
        File.WriteAllText(Path.Combine(runDir, "summary.csv"), sb.ToString());

        var cu = new StringBuilder();
        cu.AppendLine("run_tag,cell,backend,backlog_events,drain_sec,drain_events_per_sec,primary_value,replica_value,zero_loss");
        foreach (var c in catchups)
            cu.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{c.Cell},{c.Backend},{c.Backlog},{c.DrainSec:F3},{c.EventsPerSec:F0},{c.PrimaryValue},{c.ReplicaValue},{(c.ZeroLoss ? 1 : 0)}"));
        File.WriteAllText(Path.Combine(runDir, "catchup.csv"), cu.ToString());
    }

    private static void WriteHeadline(string runDir, string host, string utc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Lab 5 — offline operation (local buffer + async flush) — headline");
        sb.AppendLine();
        sb.AppendLine($"- Runtime: Pacifico `{runTag}` (built against the public mirror).");
        sb.AppendLine($"- Host: {host}");
        sb.AppendLine($"- N = {N} online appends, M = {M} partition appends, {reps} reps, warm-up = {WarmupN}.");
        sb.AppendLine($"- Appends = public `PerformCommand`; buffer selected by the `localBufferPath=` connection key; progress observers via the internal Diary grant; zero-loss = public replica `PerformQuery` over the remote.");
        sb.AppendLine($"- Run: {utc}");
        sb.AppendLine();
        sb.AppendLine("## Table 1 — per-append latency by cell × phase");
        sb.AppendLine();
        sb.AppendLine("| cell | phase | samples | mean us | p50 us | p95 us | p99 us | max us | events/sec |");
        sb.AppendLine("|------|-------|--------:|--------:|-------:|-------:|-------:|-------:|-----------:|");
        foreach (var a in aggregates)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {a.Cell} | {a.Phase} | {a.Samples} | {a.MeanUs:F2} | {a.P50Us:F2} | {a.P95Us:F2} | {a.P99Us:F2} | {a.MaxUs:F2} | {a.EventsPerSec:F0} |"));
        sb.AppendLine();
        sb.AppendLine("## Table 2 — buffer speedup (direct ÷ buffered, online p50)");
        sb.AppendLine();
        sb.AppendLine("| backend | direct p50 us | buffered p50 us | speedup |");
        sb.AppendLine("|---------|--------------:|----------------:|--------:|");
        foreach (var backend in aggregates.Select(a => a.Backend).Distinct())
        {
            var d = aggregates.FirstOrDefault(a => a.Backend == backend && a.Mode == "direct" && a.Phase == "Online");
            var b = aggregates.FirstOrDefault(a => a.Backend == backend && a.Mode == "buffered" && a.Phase == "Online");
            if (d == null || b == null) continue;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {backend} | {d.P50Us:F2} | {b.P50Us:F2} | {d.P50Us / b.P50Us:F1}x |"));
        }
        if (catchups.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Table 3 — catch-up after reconnect (buffered cells)");
            sb.AppendLine();
            sb.AppendLine("| cell | backlog | drain sec | drain events/sec | primary | replica | zero loss |");
            sb.AppendLine("|------|--------:|----------:|-----------------:|--------:|--------:|:---------:|");
            foreach (var c in catchups)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {c.Cell} | {c.Backlog} | {c.DrainSec:F2} | {c.EventsPerSec:F0} | {c.PrimaryValue} | {c.ReplicaValue} | {(c.ZeroLoss ? "yes" : "NO")} |"));
        }
        File.WriteAllText(Path.Combine(runDir, "headline.md"), sb.ToString());
    }

    private static double Micros(double ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
    private static long Median(long[] t) { long[] s = (long[])t.Clone(); Array.Sort(s); return s[s.Length / 2]; }
    private static double Pct(long[] sortedAsc, double q)
    {
        if (sortedAsc.Length == 0) return 0;
        if (sortedAsc.Length == 1) return sortedAsc[0];
        double pos = q * (sortedAsc.Length - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return lo == hi ? sortedAsc[lo] : sortedAsc[lo] * (1 - (pos - lo)) + sortedAsc[hi] * (pos - lo);
    }
    private static void TryDelete(string dir) { if (dir != null && Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } } }
    private static void ForceGc() { GC.Collect(); GC.WaitForPendingFinalizers(); }
    private static int EnvInt(string k, int dflt)
    { string v = Environment.GetEnvironmentVariable(k); return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : dflt; }
    private static string[] EnvList(string k, string[] dflt)
    { string v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? dflt : v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(); }
}
