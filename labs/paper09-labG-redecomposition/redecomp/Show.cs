using System.Text;
using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>
/// Renders the two decompositions' records as boards, side by side.
/// <para>
/// It exists because the counts this harness reports are not watchable. "0
/// divergences over 47,783 steps" is a number a reader has to trust; two boards
/// that agree are a thing a reader can look at. And the split record cannot be read
/// by any host of the example — the example's actor carries <c>Well</c> while that
/// record asks for <c>PileWell</c>, and the engine says so by name — so this is the
/// only way to see the re-cut side at all.
/// </para>
/// <para>
/// Note what is NOT written here: a renderer. Both decompositions answer
/// <see cref="IGameActor.Snapshot"/> with the same <see cref="WellSnapshot"/>, and
/// the example's own <see cref="BoardRenderer"/> draws either without knowing which
/// it holds. That the same renderer serves both is the section's claim rather than a
/// convenience of this file.
/// </para>
/// <para>
/// One subtlety, the same one the cross-check in <see cref="Replay"/> observes:
/// opening a journal WRITES to it, because the staging re-issues its seed upgrade on
/// every rehydration. So both records are read on copies and the originals are left
/// exactly as the measurement left them.
/// </para>
/// </summary>
internal static class Show
{
    internal static int Run(string originalJournal, string splitRoot)
    {
        if (!Directory.Exists(originalJournal))
        {
            Console.Error.WriteLine($"no journal at '{originalJournal}' — run `play` first.");
            return 2;
        }

        if (!Directory.Exists(Path.Combine(splitRoot, "pile")) ||
            !Directory.Exists(Path.Combine(splitRoot, "piece")))
        {
            Console.Error.WriteLine(
                $"'{splitRoot}' does not hold a pile and a piece record — run `redecompose` first.");
            return 2;
        }

        var scratch = Path.Combine(splitRoot, "boards-read");
        if (Directory.Exists(scratch)) Delete(scratch);
        Directory.CreateDirectory(scratch);

        var originalCopy = Path.Combine(scratch, "orig");
        var splitCopy = Path.Combine(scratch, "split");
        Copy(originalJournal, originalCopy);
        Copy(splitRoot, splitCopy, skip: Path.GetFileName(scratch));

        string undivided, recut;
        WellSnapshot undividedSnapshot, recutSnapshot;

        using (var original = TetrisActor.Persistent(Replay.Session, 10, 20, originalCopy))
        {
            undividedSnapshot = original.Snapshot();
            undivided = BoardRenderer.Board(undividedSnapshot, "THE UNDIVIDED WELL");
        }

        using (var split = SplitTetrisActor.Persistent("recut", 10, 20, splitCopy))
        {
            recutSnapshot = split.Snapshot();
            recut = BoardRenderer.Board(recutSnapshot, "THE PILE ROLE + THE PIECE ROLE");
        }

        Console.WriteLine();
        Console.WriteLine(SideBySide(undivided, recut));

        var same = Tetris.Redecomp.Boards.Of(undividedSnapshot) == Tetris.Redecomp.Boards.Of(recutSnapshot)
            && undividedSnapshot.ClearedLines == recutSnapshot.ClearedLines
            && undividedSnapshot.IsGameOver == recutSnapshot.IsGameOver;

        Console.WriteLine(same
            ? "The two boards are the same board. The left one is one actor's record; the right one is "
              + "two actors' records, joined by the staging that holds them — which is where a whole "
              + "board can be assembled and, per the paper's constraint, the only place it can be."
            : "THE TWO BOARDS DIFFER. That is a failure of the re-decomposition, not of this view.");

        Delete(scratch);
        return same ? 0 : 1;
    }

    /// <summary>
    /// Best-effort removal. The engine can still hold a journal file open a moment
    /// after the actor is disposed, and failing to tidy a scratch copy must not turn a
    /// successful reading into a crash — which is what an unguarded recursive delete
    /// did here, reporting the boards correctly and then throwing over a locked file.
    /// </summary>
    private static void Delete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            Console.WriteLine($"(left {directory} in place — a journal file was still open)");
        }
    }

    /// <summary>Lays two multi-line blocks out in two columns.</summary>
    private static string SideBySide(string left, string right)
    {
        var l = left.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var r = right.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var width = l.Max(line => line.Length) + 6;

        var sb = new StringBuilder();
        for (var i = 0; i < Math.Max(l.Length, r.Length); i++)
        {
            var lineL = i < l.Length ? l[i] : string.Empty;
            var lineR = i < r.Length ? r[i] : string.Empty;
            sb.AppendLine((lineL.PadRight(width) + lineR).TrimEnd());
        }

        return sb.ToString();
    }

    private static void Copy(string from, string to, string? skip = null)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            var name = Path.GetFileName(directory);
            if (name == skip) continue;
            Copy(directory, Path.Combine(to, name));
        }
    }
}
