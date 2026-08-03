using System.Collections.Generic;

namespace Tetris.Hex.Tests;

/// <summary>
/// TEST DOUBLE #2 — a stand-in for the driven port
/// <see cref="IPieceSelectionPort"/>. A stub: it hands out a scripted sequence
/// of letters so a test over the application is deterministic, repeating the
/// last letter once the script runs out.
/// <para>
/// The journaled arrangement needs no equivalent: its <c>Well.Spawn(PieceType)</c>
/// takes the piece it is told to place, so a test simply tells it, and the
/// non-determinism lives in a transient query the test can ignore. Here the
/// policy is behind a port the application depends on, so a deterministic test
/// has to supply one.
/// </para>
/// </summary>
internal sealed class ScriptedPieceSelection : IPieceSelectionPort
{
    private readonly Queue<string> script;
    private string last;

    public ScriptedPieceSelection(params string[] letters)
    {
        script = new Queue<string>(letters);
        last = letters.Length > 0 ? letters[^1] : "O";
    }

    /// <summary>A stub that always answers with the same letter.</summary>
    public static ScriptedPieceSelection Always(string letter) => new(letter);

    public string NextPieceLetter()
    {
        if (script.Count > 0)
        {
            last = script.Dequeue();
        }

        return last;
    }
}
