// Paper 5 — Lab 6: per-entry append latency, local FileSystem journal vs co-located RDBMS.
//
// Measures the latency budget of the substrate's durable append under a 1-event =
// 1-durable-commit régime, across three storage backends configured with matched
// fsync semantics:
//
//   - FileSystem : local journal, JournalWriter.AppendRecord -> Flush(flushToDisk:true)
//   - MySQL      : co-located container, innodb_flush_log_at_trx_commit=1, sync_binlog=1
//   - SQL Edge   : co-located container, default durable-log-flush per commit
//
// PUBLIC-SURFACE measurement. Each append is a single public PerformCommand of a
// compact parametric verb ("_seq = @val;") under CompiledModePolicy.AlwaysCompiled:
// the first call journals a Define + Invocation, every later call journals ONE compact
// Invocation entry (the Paper 2 compact-action régime). Stopwatch.GetTimestamp() (QPC,
// ~100 ns) brackets each PerformCommand call. The measured latency is therefore the
// full public command+append cost the application actually pays — a small, backend-
// invariant DSL constant sits above the pure storage append, so the FileSystem number
// is an upper bound on storage latency and the RDBMS/FS ratio is conservative (the
// shared DSL constant compresses it slightly). See README "Guide compliance".
//
// Usage:  dotnet run -c Release -- <outDir> <runTag> [smoke]
//         env: LAB6_BACKENDS=FileSystem,MySQL,SQLServer  LAB6_N=100000  LAB6_REPS=5

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
    private static readonly Assembly LibAssembly = typeof(ActorV2).Assembly;

    private static readonly string[] DefaultBackends = { "FileSystem", "MySQL", "SQLServer" };
    private const int DefaultN = 100_000;
    private const int DefaultReps = 5;
    private const int WarmupN = 1_000;

    // Connection strings match docker-compose.lab.yml (MySQL 3306, SQL Edge 1433).
    private const string MySqlConn =
        "server=localhost;port=3306;database=lab6_mysql;user id=root;password=puppeteer;SslMode=none;DefaultCommandTimeout=300";
    private const string MsSqlConn =
        "server=localhost,1433;database=lab6_mssql;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false";
    private const string MsSqlMasterConn =
        "server=localhost,1433;database=master;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false;Connection Timeout=10";

    private enum Backend { FileSystem, MySQL, SQLServer }

    private sealed record Agg(string Name, long Samples, double Mean, double P50, double P95, double P99, double Max, string Fsync);

    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "results");
        string runTag = args.Length > 1 ? args[1] : "unknown";
        bool smoke = args.Any(a => a.Equals("smoke", StringComparison.OrdinalIgnoreCase));

        int n = EnvInt("LAB6_N", smoke ? 2_000 : DefaultN);
        int reps = EnvInt("LAB6_REPS", smoke ? 2 : DefaultReps);
        string[] selected = EnvList("LAB6_BACKENDS", DefaultBackends);

        string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string host = $"{Environment.OSVersion} / {Environment.ProcessorCount}cores / .NET {Environment.Version}";
        string runDir = Path.Combine(outDir, $"run-{utc}-{runTag}");
        Directory.CreateDirectory(runDir);

        Console.WriteLine($"[Lab6] backends=[{string.Join(",", selected)}] N={n} reps={reps} warmup={WarmupN} runTag={runTag}");
        Console.WriteLine($"[Lab6] out={runDir}");

        var aggregates = new List<Agg>();
        string samplesPath = Path.Combine(runDir, "samples.csv");
        using (var samples = new StreamWriter(samplesPath))
        {
            samples.WriteLine("run_tag,backend,rep,event_idx,append_ticks,append_micros");

            foreach (string backendName in selected)
            {
                Backend backend = Enum.Parse<Backend>(backendName, ignoreCase: true);
                if (!Probe(backend, out string skip))
                {
                    Console.WriteLine($"[Lab6] SKIP {backendName}: {skip}");
                    continue;
                }
                Console.WriteLine($"[Lab6] === {backendName} ===");
                var allTicks = new List<long>(n * reps);
                for (int rep = 0; rep < reps; rep++)
                {
                    long[] ticks = RunCell(backend, rep, n);
                    double freq = Stopwatch.Frequency;
                    for (int i = 0; i < ticks.Length; i++)
                        samples.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"{runTag},{backendName},{rep},{i},{ticks[i]},{ticks[i] * 1_000_000.0 / freq:F3}"));
                    allTicks.AddRange(ticks);
                    Console.WriteLine($"[Lab6]   {backendName} rep {rep}: median ~= {Micros(Median(ticks)):F2} us");
                }
                aggregates.Add(Aggregate(backendName, FsyncFor(backend), allTicks));
            }
        }

        WriteSummary(Path.Combine(runDir, "summary.csv"), aggregates, runTag);
        WriteHeadline(Path.Combine(runDir, "headline.md"), aggregates, runTag, host, n, reps, utc);

        Console.WriteLine();
        Console.WriteLine($"=== Lab 6 — latency budget @ {runTag} ===");
        foreach (var a in aggregates)
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-12} p50={1,8:F2}us p95={2,9:F2}us p99={3,9:F2}us  n={4}", a.Name, a.P50, a.P95, a.P99, a.Samples));
        Console.WriteLine($"Data: {runDir}");
        return 0;
    }

    private static long[] RunCell(Backend backend, int rep, int n)
    {
        string actorName = $"lab6_{backend}_{rep}_{Guid.NewGuid():N}"[..24];
        string dataDir = null;
        string conn = BuildConn(backend, out dataDir);
        try
        {
            using var perf = new PerformanceV2(actorName, LibAssembly);
            perf.ConfigureStorage(ToDbType(backend), conn);
            perf.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            perf.Start();

            // Warm-up: JIT, page cache, connection pool, schema creation, and the
            // first Define+compact-Invocation shape — discarded.
            for (int i = 0; i < WarmupN; i++)
            {
                int v = i;
                perf.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = v; }).PerformCommand();
            }

            long[] ticks = new long[n];
            for (int i = 0; i < n; i++)
            {
                int v = WarmupN + i;
                long t0 = Stopwatch.GetTimestamp();
                perf.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = v; }).PerformCommand();
                long t1 = Stopwatch.GetTimestamp();
                ticks[i] = t1 - t0;
            }
            perf.Actor.GracefulExit();
            return ticks;
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (dataDir != null) { try { Directory.Delete(dataDir, true); } catch { } }
        }
    }

    private static string BuildConn(Backend backend, out string dataDir)
    {
        dataDir = null;
        switch (backend)
        {
            case Backend.FileSystem:
                dataDir = Path.Combine(Path.GetTempPath(), "L6_" + Guid.NewGuid().ToString("N")[..12]);
                Directory.CreateDirectory(dataDir);
                return $"path={dataDir};maxFileSize=16777216;compression=None";
            case Backend.MySQL: return MySqlConn;
            case Backend.SQLServer: return MsSqlConn;
            default: throw new InvalidOperationException();
        }
    }

    private static DatabaseType ToDbType(Backend b) => b switch
    {
        Backend.FileSystem => DatabaseType.FileSystem,
        Backend.MySQL => DatabaseType.MySQL,
        Backend.SQLServer => DatabaseType.SQLServer,
        _ => throw new InvalidOperationException()
    };

    private static string FsyncFor(Backend b) => b switch
    {
        Backend.FileSystem => "Flush(flushToDisk:true)",
        Backend.MySQL => "innodb_flush_log_at_trx_commit=1,sync_binlog=1",
        Backend.SQLServer => "default(full durability)",
        _ => "?"
    };

    // Availability probe: attempt a throwaway actor bootstrap against the backend.
    private static bool Probe(Backend backend, out string skip)
    {
        skip = null;
        if (backend == Backend.FileSystem) return true;
        string actorName = "probe_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // SQL Server family: the storage classes create tables on demand but NOT
            // the database itself. Create it once via a master connection (public
            // ADO.NET, mirrors the original harness's EnsureSqlServerDatabaseExists).
            if (backend == Backend.SQLServer)
            {
                using var master = new Microsoft.Data.SqlClient.SqlConnection(MsSqlMasterConn);
                master.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "IF DB_ID('lab6_mssql') IS NULL CREATE DATABASE lab6_mssql;", master);
                cmd.ExecuteNonQuery();
            }
            using var perf = new PerformanceV2(actorName, LibAssembly);
            perf.ConfigureStorage(ToDbType(backend), BuildConn(backend, out _));
            perf.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            perf.Start();
            perf.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = 1; }).PerformCommand();
            perf.Actor.GracefulExit();
            return true;
        }
        catch (Exception ex)
        {
            skip = ex.GetType().Name + ": " + ex.Message.Replace('\n', ' ').Replace('\r', ' ');
            if (skip.Length > 200) skip = skip[..200];
            return false;
        }
    }

    private static Agg Aggregate(string name, string fsync, List<long> ticks)
    {
        long[] sorted = ticks.ToArray();
        Array.Sort(sorted);
        double mean = sorted.Average();
        return new Agg(name, sorted.LongLength, Micros(mean),
            Micros(Percentile(sorted, 0.50)), Micros(Percentile(sorted, 0.95)),
            Micros(Percentile(sorted, 0.99)), Micros(sorted[^1]), fsync);
    }

    private static void WriteSummary(string path, List<Agg> aggs, string runTag)
    {
        var sb = new StringBuilder();
        sb.AppendLine("run_tag,backend,samples,mean_micros,p50_micros,p95_micros,p99_micros,max_micros,fsync_mode");
        foreach (var a in aggs)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{a.Name},{a.Samples},{a.Mean:F2},{a.P50:F2},{a.P95:F2},{a.P99:F2},{a.Max:F2},{a.Fsync}"));
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteHeadline(string path, List<Agg> aggs, string runTag, string host, int n, int reps, string utc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Lab 6 — latency budget (local FileSystem vs co-located RDBMS) — headline");
        sb.AppendLine();
        sb.AppendLine($"- Runtime: Pacifico `{runTag}` (built against the public mirror).");
        sb.AppendLine($"- Host: {host}");
        sb.AppendLine($"- Régime: compact-action journal, `CompiledModePolicy.AlwaysCompiled`, 1 event = 1 durable commit.");
        sb.AppendLine($"- N = {n} measured appends per rep, K = {reps} reps, warm-up = {WarmupN} discarded.");
        sb.AppendLine($"- Measurement: public `PerformCommand` bracketed by `Stopwatch.GetTimestamp()` (QPC, ~100 ns).");
        sb.AppendLine($"- Run: {utc}");
        sb.AppendLine();
        sb.AppendLine("## Table 1 — per-append latency by backend");
        sb.AppendLine();
        sb.AppendLine("| backend | samples | mean us | p50 us | p95 us | p99 us | max us | fsync mode |");
        sb.AppendLine("|---------|--------:|--------:|-------:|-------:|-------:|-------:|------------|");
        foreach (var a in aggs)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {a.Name} | {a.Samples} | {a.Mean:F2} | {a.P50:F2} | {a.P95:F2} | {a.P99:F2} | {a.Max:F2} | {a.Fsync} |"));
        var fs = aggs.FirstOrDefault(a => a.Name == "FileSystem");
        if (fs != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Table 2 — latency ratio (RDBMS / FileSystem)");
            sb.AppendLine();
            sb.AppendLine("| backend | p50 ratio | p95 ratio | mean ratio |");
            sb.AppendLine("|---------|----------:|----------:|-----------:|");
            foreach (var a in aggs)
            {
                if (a.Name == "FileSystem") continue;
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {a.Name} | {a.P50 / fs.P50:F2}x | {a.P95 / fs.P95:F2}x | {a.Mean / fs.Mean:F2}x |"));
            }
        }
        File.WriteAllText(path, sb.ToString());
    }

    private static double Micros(double ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
    private static long Median(long[] t) { long[] s = (long[])t.Clone(); Array.Sort(s); return s[s.Length / 2]; }

    private static double Percentile(long[] sortedAsc, double q)
    {
        if (sortedAsc.Length == 0) return 0;
        if (sortedAsc.Length == 1) return sortedAsc[0];
        double pos = q * (sortedAsc.Length - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sortedAsc[lo];
        return sortedAsc[lo] * (1 - (pos - lo)) + sortedAsc[hi] * (pos - lo);
    }

    private static int EnvInt(string k, int dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : dflt;
    }

    private static string[] EnvList(string k, string[] dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrWhiteSpace(v) ? dflt : v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
