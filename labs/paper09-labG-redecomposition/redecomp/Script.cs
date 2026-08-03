using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>The ops a driver can issue — the game's whole input alphabet.</summary>
internal enum Op
{
    Left,
    Right,
    Rotate,
    Tick,
    Drop,
}

/// <summary>
/// One step a driver actually took, so two runs can be compared on what they DID as
/// well as on where they ended up. A divergence in the issued sequence is itself a
/// divergence, and a more informative one than a mismatched board.
/// </summary>
internal sealed record Step(string Issued, string Board, int Cleared, bool Over, bool Awaiting, string? Type);

/// <summary>Renders a snapshot's occupied cells as one comparable string.</summary>
internal static class Boards
{
    internal static string Of(WellSnapshot snapshot) =>
        string.Join(" ", snapshot.Occupied
            .Select(c => (c.Row, c.Column))
            .OrderBy(c => c.Row).ThenBy(c => c.Column)
            .Select(c => $"{c.Row},{c.Column}"));
}
