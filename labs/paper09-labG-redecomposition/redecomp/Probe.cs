using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace Tetris.Redecomp;

/// <summary>
/// Throwaway probes for the two engine behaviours the re-decomposition depends
/// on. Kept out of the measurement path; run with `probe`.
/// </summary>
internal static class Probe
{
    /// <summary>
    /// Probe B — read an EXISTING journal read-only, without loading the domain
    /// and without rehydrating: the public introspection path
    /// (<c>ConfigureStorageForIntrospection</c> + <c>Introspection.ShowEntry</c>).
    /// </summary>
    internal static void ReadJournal(string actorName, string journalDirectory)
    {
        var reader = new PerformanceV2(actorName, typeof(TetrisDomain).Assembly);
        reader.Actor.ConfigureStorageForIntrospection(
            DatabaseType.FileSystem, $"path={journalDirectory};maxFileSize=4194304");

        Console.WriteLine($"CurrentEntryId (rehydration counter) = {reader.Actor.CurrentEntryId}");
        for (long id = 1; id <= 200; id++)
        {
            string toon;
            try
            {
                toon = reader.Actor.Introspection.ShowEntry(id);
            }
            catch (LanguageException ex)
            {
                Console.WriteLine($"--- end at {id}: {ex.Message}");
                break;
            }

            Console.WriteLine($"--- {id} ---");
            Console.WriteLine(toon);
        }
    }

    /// <summary>
    /// Probe A — does an <c>expose</c> issued from a LITERAL script command produce
    /// a reaction-matchable entry? (An expose pattern is not a pure-domain pattern,
    /// so Rule 1's ScriptEvent skip should not apply.)
    /// </summary>
    internal static void ExposeOnLiteralScript()
    {
        using var perf = new PerformanceV2("probe_expose", typeof(TetrisDomain).Assembly)
            .ConfigureStorage(DatabaseType.IN_MEMORY, "probe_expose")
            .Start();

        var hits = new List<string>();
        perf.OutputTarget(new CollectingSink(hits));

        perf.Actor.Reactions.DefineReaction("SeeExpose")
            .Job().Company().WithSharedHydration()
            .Seek("Exposed")
                .OnMatch("expose $cells cells; expose $token token;")
            .Program.Emit("print @cells 'cells', @token 'token';");

        perf.Using("well = Well(10, 20);").PerformCommand();
        perf.Using("well.Spawn('T');").PerformCommand();
        perf.Using("expose well.ClearedLines cells, 'land-1' token;").PerformCommand();

        perf.Actor.Reactions.Execute();
        Console.WriteLine($"expose-match hits = {hits.Count}");
        foreach (var hit in hits)
        {
            Console.WriteLine("  " + hit);
        }
    }

    private sealed class CollectingSink : IOutputSink
    {
        private readonly List<string> hits;

        internal CollectingSink(List<string> hits) => this.hits = hits;

        public void Push(in PushDocument document) => hits.Add(document.Document);
    }
}
