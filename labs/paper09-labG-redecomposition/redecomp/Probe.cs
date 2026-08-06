using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace Tetris.Redecomp;

/// <summary>
/// A read-only probe over an existing journal. Kept out of the measurement path.
/// <para>
/// A second probe used to live here, <c>ExposeOnLiteralScript</c>, asking whether an
/// <c>expose</c> issued from a LITERAL V1 script produced a reaction-matchable entry.
/// It has been removed along with its <c>probe-expose</c> sub-command: its question
/// is settled — the engine's Rule 1 skips a ScriptEvent for a pure-domain reaction,
/// and <c>expose</c> is rejected at the TOP LEVEL of a check-then-command's body
/// while being legal nested inside a block, which is why this lab's landing expose
/// sits inside an <c>if</c>. It was also the last V1 script in any Paper 9 lab, and
/// V1 is for legacy code; these labs are not legacy.
/// </para>
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

}
