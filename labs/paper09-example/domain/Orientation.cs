namespace Tetris;

/// <summary>
/// A rotation state, counted in quarter-turns from the piece's spawn pose. The
/// value object always holds a canonical index in <c>[0, count)</c>, where
/// <c>count</c> is how many <em>distinct</em> poses the piece has: 1 for the
/// square, 2 for the bar and the skews, 4 for the tee and the ells.
/// <para>
/// Modelling the count here — rather than hard-coding "rotate four times" — is
/// what lets each <see cref="Piece"/> subclass advertise its own symmetry and
/// have <see cref="Next"/> cycle correctly. Rotation turns in a single sense,
/// as in the classic original; cycling through it reaches every pose. An O
/// piece's orientation never leaves 0; an S piece's toggles 0↔1.
/// </para>
/// </summary>
internal readonly record struct Orientation
{
    /// <summary>How many distinct poses the owning piece has (1, 2, or 4).</summary>
    public int DistinctCount { get; }

    /// <summary>The canonical pose index, always in <c>[0, DistinctCount)</c>.</summary>
    public int Index { get; }

    private Orientation(int distinctCount, int index)
    {
        DistinctCount = distinctCount;
        Index = index;
    }

    /// <summary>The spawn pose for a piece with <paramref name="distinctCount"/> distinct orientations.</summary>
    public static Orientation Spawn(int distinctCount)
    {
        if (distinctCount is not (1 or 2 or 4))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(distinctCount),
                distinctCount,
                "A tetromino has 1, 2, or 4 distinct orientations.");
        }

        return new Orientation(distinctCount, 0);
    }

    /// <summary>
    /// The next pose in the single rotation sense, wrapping at
    /// <see cref="DistinctCount"/>. For a one-pose piece (the square) this is a
    /// no-op; cycling it repeatedly visits every pose in turn.
    /// </summary>
    public Orientation Next() =>
        new(DistinctCount, (Index + 1) % DistinctCount);

    public override string ToString() => $"{Index}/{DistinctCount}";
}
