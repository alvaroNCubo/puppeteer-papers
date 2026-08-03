namespace Tetris.Redecomp;

/// <summary>
/// Reads any journal, in a FRESH process, and reports what it holds. Kept separate
/// from the experiments so a journal can always be inspected by a process that did
/// not write it — which turned out to matter: a process that has just written a
/// journal has not necessarily flushed its tail, so reading your own writes reads a
/// shorter record than you wrote.
/// </summary>
internal static class Dump
{
    internal static int Run(string actorName, string journalDirectory, bool verbose)
    {
        var acts = JournalActs.Read(actorName, journalDirectory);
        Console.WriteLine($"{actorName}: {acts.Count} entries at {journalDirectory}");
        if (JournalActs.Gaps.Count > 0)
        {
            Console.WriteLine($"  GAPS at: {string.Join(", ", JournalActs.Gaps)}");
        }

        foreach (var group in acts.GroupBy(JournalActs.VerbOf)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var defines = group.Count(a => a.Kind == "define");
            var suffix = defines > 0 ? $"  ({defines} of them declarations)" : string.Empty;
            Console.WriteLine($"  {group.Count(),5}  {group.Key}{suffix}");
        }

        if (!verbose)
        {
            return 0;
        }

        foreach (var act in acts)
        {
            Console.WriteLine($"  {act.EntryId,4}  {act.Kind,-10}  {JournalActs.Body(act.Sentence)}");
            if (act.Arguments.Length > 0)
            {
                Console.WriteLine($"            args: {JournalActs.Collapse(act.Arguments)}");
            }

            if (act.ExposeData.Length > 0)
            {
                Console.WriteLine($"            expose: {JournalActs.Collapse(act.ExposeData)}");
            }
        }

        return 0;
    }
}
