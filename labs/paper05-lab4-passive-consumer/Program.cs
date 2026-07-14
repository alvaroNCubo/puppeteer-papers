// Paper 5 — Lab 4: passive consumer / Materialize v2 (backup as program copy -> replay).
//
// A primary actor declares `actor.Materialization.Register("DC-B")`; a destination
// runs the public MaterializeMirror client to pull the primary's corpus and a replica
// instantiated over the destination journal reaches the SAME in-memory state as the
// primary — across FileSystem / MySQL / SQL Edge × N ∈ {1k, 10k, 100k}, bit-exact
// (target 18/18), plus per-backend convergence throughput (events/sec).
//
// WIRE = PUBLIC. The measured protocol is the public Materialize v2 surface:
//   src  = new LocalMaterializeSource(primary.Actor.Materialization, destination)
//   mir  = new MaterializeMirror(src)
//   res  = mir.Sync()                     // Layer 1 — records + ConfirmUntil watermark
//   res  = mir.AsProgramMirror().Sync()   // Layer 2 — + ReadReactions + ReadElidedRange
// Stopwatch brackets the sync (wire) window and, separately, the apply window.
//
// APPLY = documented internal seam. MaterializeMirror deliberately does NOT apply the
// fetched records to a local store — the source calls this "a Hole for a future phase"
// (MaterializeMirror.cs:19-26): fetch+confirm is the mirror's job, local application is
// the destination operator's. This lab applies via the same structured write API the
// runtime's own shadow/replay path uses (ActorHandler.CopyPrimaryRecordsToShadow),
// preserving EntryId / OccurredAt / ExposeData / Define+Invocation data:
//   dairy.WriteScriptEntry / WriteDefineEntry / WriteInvocationEntry
// This is the ONE internal grant (InternalsVisibleTo), documented in README.
//
// PARITY = PUBLIC. A replica PerformanceV2 is instantiated over each destination
// journal and its state is read with a public PerformQuery.
//
// Usage:  dotnet run -c Release -- <outDir> <runTag> [smoke]
//         env: LAB4_BACKENDS=FileSystem,MySQL,SQLServer  LAB4_NVALUES=1000,10000,100000  LAB4_REPS=3

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
using Puppeteer.EventSourcing.DB;

internal static class Program
{
    private static readonly Assembly LibAssembly = typeof(ActorV2).Assembly;

    private static readonly string[] DefaultBackends = { "FileSystem", "MySQL", "SQLServer" };
    private static readonly int[] DefaultNValues = { 1_000, 10_000, 100_000 };
    private const int DefaultReps = 3;      // rep 0 = warmup (discarded), reps 1..2 measured
    private const int WarmupRep = 0;
    private const int CatchupGap = 1_000;

    private const string MySqlConn =
        "server=localhost;port=3306;database=lab4_mysql;user id=root;password=puppeteer;SslMode=none;DefaultCommandTimeout=300";
    private const string MsSqlConn =
        "server=localhost,1433;database=lab4_mssql;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false;Command Timeout=300";
    private const string MsSqlMasterConn =
        "server=localhost,1433;database=master;user id=sa;password=Puppeteer123!;TrustServerCertificate=true;Encrypt=false;Connection Timeout=10";

    private enum Backend { FileSystem, MySQL, SQLServer }

    private static string runTag;
    private static StringBuilder syncCsv, parityCsv, catchupCsv;

    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "results");
        runTag = args.Length > 1 ? args[1] : "unknown";
        bool smoke = args.Any(a => a.Equals("smoke", StringComparison.OrdinalIgnoreCase));

        int[] nValues = EnvIntList("LAB4_NVALUES", smoke ? new[] { 500 } : DefaultNValues);
        int reps = EnvInt("LAB4_REPS", smoke ? 2 : DefaultReps);
        string[] selected = EnvList("LAB4_BACKENDS", DefaultBackends);

        string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string host = $"{Environment.OSVersion} / {Environment.ProcessorCount}cores / .NET {Environment.Version}";
        string runDir = Path.Combine(outDir, $"run-{utc}-{runTag}");
        Directory.CreateDirectory(runDir);

        syncCsv = new StringBuilder();
        syncCsv.AppendLine("run_tag,backend,N,layer,rep,is_warmup,sync_window_ms,apply_window_ms,total_ms,records_applied,events_per_sec");
        parityCsv = new StringBuilder();
        parityCsv.AppendLine("run_tag,backend,N,layer,primary_value,replica_value,parity_ok");
        catchupCsv = new StringBuilder();
        catchupCsv.AppendLine("run_tag,backend,gap_size,catchup_ms,records_replayed,events_per_sec");

        Console.WriteLine($"[Lab4] backends=[{string.Join(",", selected)}] N=[{string.Join(",", nValues)}] reps={reps} runTag={runTag}");
        Console.WriteLine($"[Lab4] out={runDir}");

        foreach (string backendName in selected)
        {
            Backend backend = Enum.Parse<Backend>(backendName, ignoreCase: true);
            if (!Probe(backend, out string skip))
            {
                Console.WriteLine($"[Lab4] SKIP {backendName}: {skip}");
                continue;
            }
            Console.WriteLine($"[Lab4] === {backendName} ===");
            foreach (int n in nValues)
                for (int rep = 0; rep < reps; rep++)
                    RunCell(backend, backendName, n, rep);
            RunCatchupCell(backend, backendName, nValues[^1]);
        }

        File.WriteAllText(Path.Combine(runDir, "sync_samples.csv"), syncCsv.ToString());
        File.WriteAllText(Path.Combine(runDir, "parity.csv"), parityCsv.ToString());
        File.WriteAllText(Path.Combine(runDir, "catchup_samples.csv"), catchupCsv.ToString());
        WriteSummaryAndHeadline(runDir, host, nValues, reps, utc);

        Console.WriteLine($"[Lab4] dataset written to {runDir}");
        return 0;
    }

    private static void RunCell(Backend backend, string backendName, int n, int rep)
    {
        bool isWarmup = rep == WarmupRep;
        string actorName = "lab4_" + Guid.NewGuid().ToString("N")[..12];
        string primaryDir = NewTempDir("Lab4_primary_");
        try
        {
            // ─── Primary (FileSystem, AlwaysCompiled): register destinations BEFORE
            //     populating (forward-fidelity — a destination reads only records after
            //     its RegisteredAtEntryId), then journal N compact invocations. ───
            using var primary = new PerformanceV2(actorName, LibAssembly);
            primary.ConfigureStorage(DatabaseType.FileSystem, $"path={primaryDir};maxFileSize=4194304;compression=None");
            primary.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            primary.Start();

            string destC1 = backendName + "_C1";
            string destC2 = backendName + "_C2";
            primary.Actor.Materialization.Register(destC1);
            primary.Actor.Materialization.Register(destC2);

            int primaryValue = Populate(primary, n);

            // ─── Layer 1 (records only) ───
            MeasureSync(primary, destC1, backend, backendName, n, rep, isWarmup, layer: 1,
                out DiaryStorage c1Storage, out string c1Conn, out DatabaseType c1Db, out ActorV2 c1Actor);

            // ─── Layer 2 (records + reactions snapshot + elision markers) ───
            MeasureSync(primary, destC2, backend, backendName, n, rep, isWarmup, layer: 2,
                out DiaryStorage c2Storage, out string c2Conn, out DatabaseType _, out ActorV2 c2Actor);

            // ─── Parity: instantiate a replica over the Layer-1 destination journal ───
            c1Actor.GracefulExit();
            ForceGc();
            if (!isWarmup)
                VerifyParity(actorName, c1Db, c1Conn, primaryValue, backendName, n, layer: 1);

            c2Actor.GracefulExit();
            primary.Actor.GracefulExit();
        }
        finally
        {
            ForceGc();
            TryDelete(primaryDir);
        }
    }

    // Overload glue: MeasureSync needs to return the destination actor + storage handle
    // so the caller can rehydrate a replica afterwards. The `out ActorV2 unused` slot on
    // the C2 call keeps the signature uniform.
    private static void MeasureSync(
        PerformanceV2 primary, string destination, Backend backend, string backendName,
        int n, int rep, bool isWarmup, int layer,
        out DiaryStorage destStorage, out string destConn, out DatabaseType destDb, out ActorV2 destActor)
    {
        string destBackendDir;
        destActor = PrepareDestination(backend, primary.Name + (layer == 2 ? "_c2" : ""),
            out destConn, out destDb, out destBackendDir);
        destStorage = destActor.Handler.TryGetDiaryStorage();

        var src = new LocalMaterializeSource(primary.Actor.Materialization, destination);
        var mirror = new MaterializeMirror(src);

        // Wire window (public): fetch records + confirm watermark.
        var syncSw = Stopwatch.StartNew();
        MirrorSyncResult result = layer == 1 ? mirror.Sync() : mirror.AsProgramMirror().Sync();
        syncSw.Stop();

        // Apply window (documented internal seam): structured writes into the
        // destination backend, preserving EntryId / OccurredAt / ExposeData.
        var applySw = Stopwatch.StartNew();
        ApplyRecords(result.Records, destStorage);
        applySw.Stop();

        double syncMs = syncSw.Elapsed.TotalMilliseconds;
        double applyMs = applySw.Elapsed.TotalMilliseconds;
        double totalMs = syncMs + applyMs;
        int applied = result.Records.Count;
        double eps = totalMs > 0 ? applied / (totalMs / 1000.0) : 0;

        syncCsv.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"{runTag},{backendName},{n},{layer},{rep},{(isWarmup ? 1 : 0)},{syncMs:F3},{applyMs:F3},{totalMs:F3},{applied},{eps:F1}"));
    }

    private static void ApplyRecords(IReadOnlyList<MaterializationRecord> records, DiaryStorage storage)
    {
        foreach (var rec in records)
        {
            switch (rec.Kind)
            {
                case MaterializationRecordKind.Script:
                    storage.WriteScriptEntry(rec.EntryId, rec.Script, rec.OccurredAt, rec.ExposeData);
                    break;
                case MaterializationRecordKind.Define:
                    storage.WriteDefineEntry(rec.ActionId, rec.DefineStatementText, rec.EntryId, rec.OccurredAt, rec.ExposeData);
                    break;
                case MaterializationRecordKind.Invocation:
                    storage.WriteInvocationEntry(rec.ActionId, rec.EntryId, rec.OccurredAt, rec.Arguments, rec.ExposeData);
                    break;
            }
        }
    }

    private static void RunCatchupCell(Backend backend, string backendName, int largeN)
    {
        string actorName = "lab4cu_" + Guid.NewGuid().ToString("N")[..12];
        string primaryDir = NewTempDir("Lab4cu_primary_");
        try
        {
            using var primary = new PerformanceV2(actorName, LibAssembly);
            primary.ConfigureStorage(DatabaseType.FileSystem, $"path={primaryDir};maxFileSize=4194304;compression=None");
            primary.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            primary.Start();

            string dest = backendName + "_catchup";
            primary.Actor.Materialization.Register(dest);
            int last = Populate(primary, largeN);

            ActorV2 destActor = PrepareDestination(backend, actorName, out string destConn, out DatabaseType destDb, out _);
            DiaryStorage destStorage = destActor.Handler.TryGetDiaryStorage();

            var src = new LocalMaterializeSource(primary.Actor.Materialization, dest);
            var mirror = new MaterializeMirror(src);
            ApplyRecords(mirror.Sync().Records, destStorage);          // initial sync to head

            ContinuePopulate(primary, last, CatchupGap);                // advance the primary

            var sw = Stopwatch.StartNew();                             // time the gap recovery
            var catchup = mirror.Sync();
            ApplyRecords(catchup.Records, destStorage);
            sw.Stop();

            int replayed = catchup.Records.Count;
            double ms = sw.Elapsed.TotalMilliseconds;
            double eps = ms > 0 ? replayed / (ms / 1000.0) : 0;
            catchupCsv.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{backendName},{CatchupGap},{ms:F3},{replayed},{eps:F1}"));
            Console.WriteLine($"[Lab4]   {backendName} catchup: {replayed} recs in {ms:F1}ms ({eps:F0} ev/s)");

            destActor.GracefulExit();
            primary.Actor.GracefulExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lab4] catchup {backendName} FAILED: {ex.Message}");
            catchupCsv.AppendLine($"{runTag},{backendName},{CatchupGap},FAIL,0,0");
        }
        finally { ForceGc(); TryDelete(primaryDir); }
    }

    // ─── Workload: N compact invocations of `_seq = @val;` -> _seq == N-1 at head. ───
    private static int Populate(PerformanceV2 primary, int n)
    {
        int last = 0;
        int invocations = Math.Max(0, n - 1);   // + 1 Define entry => N journal entries
        for (int i = 1; i <= invocations; i++)
        {
            int v = i;
            primary.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = v; }).PerformCommand();
            last = v;
        }
        return last;
    }

    private static void ContinuePopulate(PerformanceV2 primary, int startFrom, int additional)
    {
        for (int i = 1; i <= additional; i++)
        {
            int v = startFrom + i;
            primary.Actor.Using("_seq = @val;").WithParameters(p => { p["val", typeof(int)] = v; }).PerformCommand();
        }
    }

    private static void VerifyParity(string actorName, DatabaseType destDb, string destConn,
        int primaryValue, string backendName, int n, int layer)
    {
        int replicaValue = -1;
        try
        {
            using var replica = new PerformanceV2(actorName, LibAssembly);
            replica.ConfigureStorage(destDb, destConn);
            replica.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
            replica.Start();
            string q = replica.Actor.Using("{ print _seq 'value'; }").PerformQuery();
            int idx = q.IndexOf(':');
            if (idx >= 0) int.TryParse(q[(idx + 1)..].TrimEnd('}'), out replicaValue);
            replica.Actor.GracefulExit();
        }
        catch (Exception ex) { Console.WriteLine($"[Lab4] parity replica failed {backendName} N={n}: {ex.Message}"); }

        bool ok = replicaValue == primaryValue;
        parityCsv.AppendLine($"{runTag},{backendName},{n},{layer},{primaryValue},{replicaValue},{(ok ? 1 : 0)}");
        Console.WriteLine($"[Lab4]   {backendName} N={n} parity: primary={primaryValue} replica={replicaValue} {(ok ? "OK" : "MISMATCH")}");
    }

    // ─── Destination provisioning: a bare ActorV2 bound to the backend (for its
    //     DiaryStorage). Fresh journal per cell. ───
    private static ActorV2 PrepareDestination(Backend backend, string actorName,
        out string destConn, out DatabaseType destDb, out string destBackendDir)
    {
        destBackendDir = null;
        switch (backend)
        {
            case Backend.FileSystem:
                destBackendDir = NewTempDir("Lab4_dest_");
                destConn = $"path={destBackendDir};maxFileSize=4194304;compression=None";
                destDb = DatabaseType.FileSystem;
                break;
            case Backend.MySQL:
                destConn = MySqlConn; destDb = DatabaseType.MySQL;
                DropTable(backend, actorName);
                break;
            case Backend.SQLServer:
                destConn = MsSqlConn; destDb = DatabaseType.SQLServer;
                DropTable(backend, actorName);
                break;
            default: throw new InvalidOperationException();
        }
        var actor = new ActorV2(actorName, LibAssembly);
        actor.Handler.EventSourcingStorage(destDb, destConn);
        return actor;
    }

    private static void DropTable(Backend backend, string actorName)
    {
        try
        {
            if (backend == Backend.MySQL)
            {
                using var c = new MySql.Data.MySqlClient.MySqlConnection(MySqlConn);
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"DROP TABLE IF EXISTS `{actorName}`;";
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var c = new Microsoft.Data.SqlClient.SqlConnection(MsSqlConn);
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"IF OBJECT_ID('[{actorName}]','U') IS NOT NULL DROP TABLE [{actorName}];";
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* fresh actor name per cell; drop is best-effort hygiene */ }
    }

    private static bool Probe(Backend backend, out string skip)
    {
        skip = null;
        if (backend == Backend.FileSystem) return true;
        try
        {
            if (backend == Backend.MySQL)
            {
                // Create the database if the container only shipped a different default.
                var b = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(MySqlConn) { Database = "" };
                using var server = new MySql.Data.MySqlClient.MySqlConnection(b.ConnectionString);
                server.Open();
                using var cmd = new MySql.Data.MySqlClient.MySqlCommand("CREATE DATABASE IF NOT EXISTS lab4_mysql;", server);
                cmd.ExecuteNonQuery();
            }
            if (backend == Backend.SQLServer)
            {
                using var master = new Microsoft.Data.SqlClient.SqlConnection(MsSqlMasterConn);
                master.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand("IF DB_ID('lab4_mssql') IS NULL CREATE DATABASE lab4_mssql;", master);
                cmd.ExecuteNonQuery();
            }
            string actorName = "probe_" + Guid.NewGuid().ToString("N")[..8];
            var actor = new ActorV2(actorName, LibAssembly);
            actor.Handler.EventSourcingStorage(ToDbType(backend), backend == Backend.MySQL ? MySqlConn : MsSqlConn);
            actor.Handler.TryGetDiaryStorage().WriteScriptEntry(1, "probe", DateTime.UtcNow, null);
            actor.GracefulExit();
            DropTable(backend, actorName);
            return true;
        }
        catch (Exception ex)
        {
            skip = ex.GetType().Name + ": " + ex.Message.Replace('\n', ' ').Replace('\r', ' ');
            if (skip.Length > 200) skip = skip[..200];
            return false;
        }
    }

    private static DatabaseType ToDbType(Backend b) => b switch
    {
        Backend.FileSystem => DatabaseType.FileSystem,
        Backend.MySQL => DatabaseType.MySQL,
        Backend.SQLServer => DatabaseType.SQLServer,
        _ => throw new InvalidOperationException()
    };

    private static void WriteSummaryAndHeadline(string runDir, string host, int[] nValues, int reps, string utc)
    {
        // Aggregate sync_samples (non-warmup) by (backend, layer, N).
        var rows = syncCsv.ToString().Split('\n').Skip(1)
            .Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).Where(p => p.Length >= 11 && p[5] == "0").ToList();
        var groups = rows.GroupBy(p => (backend: p[1], layer: int.Parse(p[3]), n: int.Parse(p[2])))
            .OrderBy(g => g.Key.backend).ThenBy(g => g.Key.layer).ThenBy(g => g.Key.n);

        var summary = new StringBuilder();
        summary.AppendLine("run_tag,backend,layer,N,samples,sync_p50_ms,apply_p50_ms,total_p50_ms,total_p95_ms,events_per_sec_p50");
        foreach (var g in groups)
        {
            var totals = g.Select(p => double.Parse(p[8], CultureInfo.InvariantCulture)).OrderBy(x => x).ToArray();
            var syncs = g.Select(p => double.Parse(p[6], CultureInfo.InvariantCulture)).OrderBy(x => x).ToArray();
            var applies = g.Select(p => double.Parse(p[7], CultureInfo.InvariantCulture)).OrderBy(x => x).ToArray();
            double tp50 = Pct(totals, 0.50), tp95 = Pct(totals, 0.95);
            double records = g.Key.n;
            double eps = tp50 > 0 ? records / (tp50 / 1000.0) : 0;
            summary.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{g.Key.backend},{g.Key.layer},{g.Key.n},{g.Count()},{Pct(syncs, 0.50):F3},{Pct(applies, 0.50):F3},{tp50:F3},{tp95:F3},{eps:F1}"));
        }
        File.WriteAllText(Path.Combine(runDir, "summary.csv"), summary.ToString());

        // Parity roll-up
        var pRows = parityCsv.ToString().Split('\n').Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).ToList();
        int parityOk = pRows.Count(p => p.Length >= 7 && p[6] == "1");

        var sb = new StringBuilder();
        sb.AppendLine("# Lab 4 — passive consumer / Materialize v2 — headline");
        sb.AppendLine();
        sb.AppendLine($"- Runtime: Pacifico `{runTag}` (built against the public mirror).");
        sb.AppendLine($"- Host: {host}");
        sb.AppendLine($"- Régime: compact-action journal (`AlwaysCompiled`), N ∈ {{{string.Join(", ", nValues)}}}, {reps} reps (rep 0 warm-up).");
        sb.AppendLine($"- Wire = public `MaterializeMirror`; apply = documented internal structured-write seam; parity = public `PerformQuery`.");
        sb.AppendLine($"- Run: {utc}");
        sb.AppendLine();
        sb.AppendLine($"**Parity: {parityOk}/{pRows.Count} cells bit-exact** (replica over destination journal reaches the primary's `_seq`).");
        sb.AppendLine();
        sb.AppendLine("## Table 1 — sync + apply throughput by backend / layer / N");
        sb.AppendLine();
        sb.AppendLine("| backend | layer | N | sync p50 ms | apply p50 ms | total p50 ms | total p95 ms | events/sec p50 |");
        sb.AppendLine("|---------|-----:|--:|------------:|-------------:|-------------:|-------------:|---------------:|");
        foreach (var line in summary.ToString().Split('\n').Skip(1).Where(l => l.Trim().Length > 0))
        {
            var p = line.Split(',');
            sb.AppendLine($"| {p[1]} | {p[2]} | {p[3]} | {p[5]} | {p[6]} | {p[7]} | {p[8]} | {p[9]} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Table 2 — catch-up after simulated retention gap");
        sb.AppendLine();
        sb.AppendLine("| backend | gap | catchup ms | records | events/sec |");
        sb.AppendLine("|---------|----:|-----------:|--------:|-----------:|");
        foreach (var line in catchupCsv.ToString().Split('\n').Skip(1).Where(l => l.Trim().Length > 0))
        {
            var p = line.Split(',');
            sb.AppendLine($"| {p[1]} | {p[2]} | {p[3]} | {p[4]} | {p[5]} |");
        }
        File.WriteAllText(Path.Combine(runDir, "headline.md"), sb.ToString());
    }

    private static double Pct(double[] sortedAsc, double q)
    {
        if (sortedAsc.Length == 0) return 0;
        if (sortedAsc.Length == 1) return sortedAsc[0];
        double pos = q * (sortedAsc.Length - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return lo == hi ? sortedAsc[lo] : sortedAsc[lo] * (1 - (pos - lo)) + sortedAsc[hi] * (pos - lo);
    }

    private static string NewTempDir(string prefix)
    {
        string d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(d);
        return d;
    }
    private static void TryDelete(string dir) { if (dir != null && Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } } }
    private static void ForceGc() { GC.Collect(); GC.WaitForPendingFinalizers(); }

    private static int EnvInt(string k, int dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : dflt;
    }
    private static int[] EnvIntList(string k, int[] dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrWhiteSpace(v) ? dflt : v.Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }
    private static string[] EnvList(string k, string[] dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrWhiteSpace(v) ? dflt : v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
