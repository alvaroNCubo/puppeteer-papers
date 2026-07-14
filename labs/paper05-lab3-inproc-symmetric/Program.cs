// Paper 5 — Lab 3: in-proc symmetric consumer (§5.2 part 2 — the remote consumer is
// a pure function of the journal prefix).
//
// Two instances of the SAME actor binary consume the SAME event stream and produce
// byte-identical output:
//   A = primary. Drives ~N events as compiled Actions in shopping-cart cycles (per order:
//       ItemsPerOrder Adds one at a time, then one Checkout that closes the order), then
//       replays its own journal through two Job reactions: a single-seek Emit (one push
//       per item added) and a `.Many()` existential elide (each checked-out order elides
//       the adds it accumulated). The elide is the everyday "buy N, pay once" shape.
//   B = passive consumer. Its journal is BUILT by materializing A's corpus via the
//       public MaterializeMirror surface, then it replays that journal through the
//       IDENTICAL reactions.
//
// The lab asserts symmetry on three axes, all captured from the PUBLIC surface:
//   (1) Emit terminator output — captured via a public IOutputSink on each side; the
//       ordered (EntryId, ReactionName, Document) stream must be byte-identical.
//   (2) Elide terminator — compared via the public introspection surface
//       (actor.Introspection.ShowReaction), normalised to drop wall-clock fields.
//   (3) Journal segments — journal_*.bin bytes of A vs B (plain file reads).
//
// FEED = public MaterializeMirror (A registers "B"; B pulls A's records).
// APPLY = documented internal structured-write seam (same as L4; local application is a
//         documented Hole in MaterializeMirror.cs:19-26) — the ONE internal grant.
// Tell terminator is OUT OF SCOPE (it would re-journal an envelope on the follower,
// breaking journal parity) — see README caveat.
//
// Usage:  dotnet run -c Release -- <outDir> <runTag> [smoke]
//         env: LAB3_NVALUES=100,500,1000  LAB3_REPS=2

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
using Lab05L3InprocSymmetric;

internal static class Program
{
    private static readonly Assembly LibAssembly = typeof(Cart).Assembly;
    private static readonly int[] DefaultNValues = { 100, 500, 1000 };
    private const int DefaultReps = 2;
    private const int ItemsPerOrder = 10;   // one cycle = one order: this many Adds + one Checkout

    // ─── Public IOutputSink: captures the Emit terminator push stream. ───
    private sealed class CaptureSink : IOutputSink
    {
        public readonly List<(long EntryId, string Reaction, string Document)> Rows = new();
        public void Push(in PushDocument d) => Rows.Add((d.EntryId, d.ReactionName, d.Document));
        public string Canonical()
        {
            var sb = new StringBuilder();
            foreach (var r in Rows.OrderBy(x => x.EntryId).ThenBy(x => x.Reaction, StringComparer.Ordinal))
                sb.Append(r.EntryId).Append('|').Append(r.Reaction).Append('|').Append(r.Document).Append('\n');
            return sb.ToString();
        }
    }

    private sealed record Cell(int N, int Rep, int EmitA, int EmitB, int CallbackByteDiff,
        int SegmentByteDiff, int ElideDiff, double WallMs);

    private static string runTag;

    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "results");
        runTag = args.Length > 1 ? args[1] : "unknown";
        bool smoke = args.Any(a => a.Equals("smoke", StringComparison.OrdinalIgnoreCase));

        int[] nValues = EnvIntList("LAB3_NVALUES", smoke ? new[] { 100 } : DefaultNValues);
        int reps = EnvInt("LAB3_REPS", smoke ? 1 : DefaultReps);

        string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string host = $"{Environment.OSVersion} / {Environment.ProcessorCount}cores / .NET {Environment.Version}";
        string runDir = Path.Combine(outDir, $"run-{utc}-{runTag}");
        Directory.CreateDirectory(runDir);

        Console.WriteLine($"[Lab3] N=[{string.Join(",", nValues)}] reps={reps} runTag={runTag}");
        Console.WriteLine($"[Lab3] out={runDir}");

        var cells = new List<Cell>();
        foreach (int n in nValues)
            for (int rep = 0; rep < reps; rep++)
            {
                Cell c = RunCell(n, rep);
                cells.Add(c);
                Console.WriteLine($"[Lab3] N={n} rep={rep}: emitA={c.EmitA} emitB={c.EmitB} cbDiff={c.CallbackByteDiff} segDiff={c.SegmentByteDiff} elideDiff={c.ElideDiff} wall={c.WallMs:F0}ms");
            }

        WriteDatasets(runDir, cells, host, nValues, reps, utc);
        int failed = cells.Count(c => c.CallbackByteDiff != 0 || c.SegmentByteDiff != 0 || c.ElideDiff != 0);
        Console.WriteLine();
        Console.WriteLine($"=== Lab 3 — symmetric consumer @ {runTag} ===  {(failed == 0 ? "ALL CELLS PARITY-CLEAN" : failed + " CELL(S) DIVERGED")}");
        Console.WriteLine($"Data: {runDir}");
        return failed == 0 ? 0 : 1;
    }

    private static Cell RunCell(int n, int rep)
    {
        string actorName = "cart_" + Guid.NewGuid().ToString("N")[..8];
        string dirA = NewTempDir("Lab3_A_");
        string dirB = NewTempDir("Lab3_B_");
        var sw = Stopwatch.StartNew();
        try
        {
            // ═══ A: produce the journal, then replay it through Job reactions. ═══
            var sinkA = new CaptureSink();
            string elideShowA;
            using (var A = new PerformanceV2(actorName, LibAssembly))
            {
                A.ConfigureStorage(DatabaseType.FileSystem, $"path={dirA};maxFileSize=16777216;compression=None");
                A.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
                A.Start();
                A.Actor.Materialization.Register("B");                       // BEFORE producing (forward-fidelity)

                A.Actor.Using("cart = Cart();").WithParameters(p => { }).PerformCommand();
                int orders = Math.Max(1, n / (ItemsPerOrder + 1));   // one cycle = one order: ItemsPerOrder Adds + 1 Checkout
                for (int o = 0; o < orders; o++)
                {
                    string order = $"A{o}";
                    for (int k = 0; k < ItemsPerOrder; k++)
                    {
                        string item = $"{order}i{k}";
                        A.Actor.Using("cart.Add(@order, @item);")
                            .WithParameters(p => { p["order", typeof(string)] = order; p["item", typeof(string)] = item; }).PerformCommand();
                    }
                    A.Actor.Using("cart.Checkout(@order);")
                        .WithParameters(p => { p["order", typeof(string)] = order; }).PerformCommand();
                }

                DefineReactions(A.Actor);
                A.OutputTarget(sinkA);
                A.Actor.Reactions.Execute();
                elideShowA = SafeShowReaction(A.Actor, "CartCheckoutElides");
                A.Actor.GracefulExit();
            }

            // ═══ Feed B from A via the public MaterializeMirror surface. ═══
            List<MaterializationRecord> records;
            using (var Aro = new PerformanceV2(actorName, LibAssembly))
            {
                Aro.ConfigureStorage(DatabaseType.FileSystem, $"path={dirA};maxFileSize=16777216;compression=None");
                Aro.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
                Aro.Start();
                var mirror = new MaterializeMirror(new LocalMaterializeSource(Aro.Actor.Materialization, "B"));
                records = mirror.AsProgramMirror().Sync().Records.ToList();
                Aro.Actor.GracefulExit();
            }

            // Apply A's corpus into B's journal via the destination ActorV2's storage
            // seam. This is the one documented internal grant (persisting raw materialized
            // records has no public API); everything else is public. ActorV2 only —
            // ActorV1 is prohibited. ActorV2 is not IDisposable, so scope it in a plain
            // block and release it with GracefulExit().
            {
                var Bboot = new ActorV2(actorName, LibAssembly);
                Bboot.Handler.EventSourcingStorage(DatabaseType.FileSystem, $"path={dirB};maxFileSize=16777216;compression=None");
                DiaryStorage storage = Bboot.Handler.TryGetDiaryStorage();
                foreach (var rec in records)
                {
                    switch (rec.Kind)
                    {
                        case MaterializationRecordKind.Script:
                            storage.WriteScriptEntry(rec.EntryId, rec.Script, rec.OccurredAt, rec.ExposeData); break;
                        case MaterializationRecordKind.Define:
                            storage.WriteDefineEntry(rec.ActionId, rec.DefineStatementText, rec.EntryId, rec.OccurredAt, rec.ExposeData); break;
                        case MaterializationRecordKind.Invocation:
                            storage.WriteInvocationEntry(rec.ActionId, rec.EntryId, rec.OccurredAt, rec.Arguments, rec.ExposeData); break;
                    }
                }
                Bboot.GracefulExit();
            }

            // ═══ B: replay the materialized journal through the IDENTICAL reactions. ═══
            var sinkB = new CaptureSink();
            string elideShowB;
            using (var B = new PerformanceV2(actorName, LibAssembly))
            {
                B.ConfigureStorage(DatabaseType.FileSystem, $"path={dirB};maxFileSize=16777216;compression=None");
                B.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled;
                B.Start();                                                   // rehydrate from the materialized journal
                DefineReactions(B.Actor);
                B.OutputTarget(sinkB);
                B.Actor.Reactions.Execute();
                elideShowB = SafeShowReaction(B.Actor, "CartCheckoutElides");
                B.Actor.GracefulExit();
            }

            ForceGc();
            int cbDiff = ByteDiff(sinkA.Canonical(), sinkB.Canonical());
            int segDiff = JournalSegmentByteDiff(dirA, dirB);
            int elideDiff = ByteDiff(Normalize(elideShowA), Normalize(elideShowB));
            sw.Stop();
            return new Cell(n, rep, sinkA.Rows.Count, sinkB.Rows.Count, cbDiff, segDiff, elideDiff, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ForceGc();
            TryDelete(dirA); TryDelete(dirB);
        }
    }

    private static void DefineReactions(ActorV2 actor)
    {
        // Wildcard binding `[_:Cart]` (not `[c:Cart]`) so the matched-parameters hash does
        // not depend on the mutating `cart` symbol (A mutates it; B is a pure replay) —
        // keeps the Emit output a pure function of the journal prefix.
        // Single-seek Emit: one push per item added — the correct, ~40 µs/event shape.
        actor.Reactions.DefineReaction("ItemAddedEmits")
            .Job().Company().WithSharedHydration()
            .Seek("AddSeek").OnMatch("[_:Cart].Add($order, $item)")
            .Program.Emit("print @item 'item';");

        // Elide in the everyday cart shape: the shopper Adds items one at a time, then a
        // single Checkout closes the order. `.Many()` is the existential quantifier — it
        // accumulates all the Adds of an order into one trajectory (collapsing the
        // multiplicity). The closing Checkout is a `.One()` ThenFinalSeek: an order is
        // checked out exactly once, so the matcher fires the elide on that single event and
        // closes the trajectory's cursors immediately (no scan past it). `$order` correlates
        // each order's adds with its own checkout, so N independent orders each elide on
        // their own close.
        actor.Reactions.DefineReaction("CartCheckoutElides")
            .Job().Company().WithSharedHydration()
            .Seek("Adds").OnMatch("[_:Cart].Add($order, $item)").Many()
            .ThenFinalSeek("CheckedOut").OnMatch("[_:Cart].Checkout($order)").One()
            .Metadata.Elide();
    }

    private static string SafeShowReaction(ActorV2 actor, string name)
    {
        try { return actor.Introspection.ShowReaction(name); }
        catch (Exception ex) { return "ERR:" + ex.GetType().Name; }
    }

    // Normalise the introspection Toon: drop wall-clock lines (occurredAt / at) so the
    // comparison is over the structural reaction state, not local timestamps.
    private static string Normalize(string toon)
    {
        if (string.IsNullOrEmpty(toon)) return "";
        var kept = toon.Replace("\r", "").Split('\n')
            .Where(l => { string t = l.TrimStart(); return !t.StartsWith("occurredAt", StringComparison.OrdinalIgnoreCase) && !t.StartsWith("at:", StringComparison.OrdinalIgnoreCase); });
        return string.Join("\n", kept);
    }

    private static int ByteDiff(string a, string b)
    {
        byte[] ba = Encoding.UTF8.GetBytes(a ?? ""), bb = Encoding.UTF8.GetBytes(b ?? "");
        int len = Math.Min(ba.Length, bb.Length), d = 0;
        for (int i = 0; i < len; i++) if (ba[i] != bb[i]) d++;
        return d + Math.Abs(ba.Length - bb.Length);
    }

    private static int JournalSegmentByteDiff(string dirA, string dirB)
    {
        var fa = Directory.GetFiles(dirA, "journal_*.bin", SearchOption.AllDirectories)
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
        var fb = Directory.GetFiles(dirB, "journal_*.bin", SearchOption.AllDirectories)
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
        int total = 0, min = Math.Min(fa.Count, fb.Count);
        for (int i = 0; i < min; i++)
        {
            byte[] a = ReadShared(fa[i]), b = ReadShared(fb[i]);
            int len = Math.Min(a.Length, b.Length);
            for (int j = 0; j < len; j++) if (a[j] != b[j]) total++;
            total += Math.Abs(a.Length - b.Length);
        }
        if (fa.Count != fb.Count) total += 1;
        return total;
    }

    private static byte[] ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    private static void WriteDatasets(string runDir, List<Cell> cells, string host, int[] nValues, int reps, string utc)
    {
        var samples = new StringBuilder();
        samples.AppendLine("run_tag,N,rep,emit_a,emit_b,callback_byte_diff,segment_byte_diff,elide_show_diff,wall_ms");
        foreach (var c in cells)
            samples.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{runTag},{c.N},{c.Rep},{c.EmitA},{c.EmitB},{c.CallbackByteDiff},{c.SegmentByteDiff},{c.ElideDiff},{c.WallMs:F1}"));
        File.WriteAllText(Path.Combine(runDir, "samples.csv"), samples.ToString());

        var summary = new StringBuilder();
        summary.AppendLine("run_tag,N,reps,callback_diff_max,segment_diff_max,elide_diff_max,emit_a_total");
        foreach (var g in cells.GroupBy(c => c.N).OrderBy(g => g.Key))
            summary.AppendLine($"{runTag},{g.Key},{g.Count()},{g.Max(c => c.CallbackByteDiff)},{g.Max(c => c.SegmentByteDiff)},{g.Max(c => c.ElideDiff)},{g.Max(c => c.EmitA)}");
        File.WriteAllText(Path.Combine(runDir, "summary.csv"), summary.ToString());

        int failed = cells.Count(c => c.CallbackByteDiff != 0 || c.SegmentByteDiff != 0 || c.ElideDiff != 0);
        var sb = new StringBuilder();
        sb.AppendLine("# Lab 3 — in-proc symmetric consumer — headline");
        sb.AppendLine();
        sb.AppendLine($"- Runtime: Pacifico `{runTag}` (built against the public mirror).");
        sb.AppendLine($"- Host: {host}");
        sb.AppendLine($"- N ∈ {{{string.Join(", ", nValues)}}} × {reps} reps.");
        sb.AppendLine($"- Feed = public `MaterializeMirror.AsProgramMirror().Sync()`; reactions = public `.Program.Emit` / `.Metadata.Elide`; Emit captured via a public `IOutputSink`; elision via `actor.Introspection.ShowReaction`; journal parity via `journal_*.bin` byte compare.");
        sb.AppendLine($"- Run: {utc}");
        sb.AppendLine();
        sb.AppendLine($"**{cells.Count - failed}/{cells.Count} cells parity-clean** (0 Emit-byte diffs, 0 journal-segment byte diffs, 0 elide-state diffs).");
        sb.AppendLine();
        sb.AppendLine("## Table — parity per N");
        sb.AppendLine();
        sb.AppendLine("| N | reps | callback_diff_max | segment_diff_max | elide_diff_max | emit events |");
        sb.AppendLine("|--:|-----:|------------------:|-----------------:|---------------:|------------:|");
        foreach (var g in cells.GroupBy(c => c.N).OrderBy(g => g.Key))
            sb.AppendLine($"| {g.Key} | {g.Count()} | {g.Max(c => c.CallbackByteDiff)} | {g.Max(c => c.SegmentByteDiff)} | {g.Max(c => c.ElideDiff)} | {g.Max(c => c.EmitA)} |");
        sb.AppendLine();
        sb.AppendLine("> Caveat: the **Tell** terminator is out of scope — symmetric Tell on B would re-journal an envelope, breaking journal-segment parity. Emit + Elide are the follower-safe terminators measured here.");
        File.WriteAllText(Path.Combine(runDir, "headline.md"), sb.ToString());
    }

    private static string NewTempDir(string prefix)
    {
        string d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(d);
        return d;
    }
    private static void TryDelete(string dir) { if (dir != null && Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } } }
    private static void ForceGc() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private static int EnvInt(string k, int dflt)
    { string v = Environment.GetEnvironmentVariable(k); return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : dflt; }
    private static int[] EnvIntList(string k, int[] dflt)
    { string v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? dflt : v.Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray(); }
}
