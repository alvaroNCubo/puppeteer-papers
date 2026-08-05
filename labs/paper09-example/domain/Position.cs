namespace Tetris;

/// <summary>
/// An immutable cell coordinate in the well's grid.
/// <para>
/// The well is read like text: <see cref="Row"/> grows downward (row 0 is the
/// ceiling) and <see cref="Column"/> grows rightward (column 0 is the left
/// wall). "Down" is therefore <c>Row + 1</c> — the direction a piece falls.
/// </para>
/// <para>
/// A value object: two positions are equal exactly when their coordinates
/// match, and every transformation returns a fresh instance. Nothing about a
/// <see cref="Position"/> ever mutates, which is what lets the spatial model
/// be replayed deterministically.
/// </para>
/// </summary>
internal readonly record struct Position(int Row, int Column)
{
    /// <summary>Returns the position shifted by <paramref name="offset"/>.</summary>
    public Position Translate(Offset offset) =>
        new(Row + offset.Rows, Column + offset.Columns);

    public override string ToString() => $"({Row}, {Column})";
}

/// <summary>
/// An immutable translation in the grid: how many rows and columns to move.
/// Decoupling the displacement from the point keeps <see cref="Position"/>
/// honest (a point is not a vector) and gives the move verbs a vocabulary —
/// <see cref="Down"/>, <see cref="Left"/>, <see cref="Right"/>.
/// </summary>
internal readonly record struct Offset(int Rows, int Columns)
{
    /// <summary>One row toward the floor — the direction of a fall.</summary>
    public static readonly Offset Down = new(1, 0);

    /// <summary>One column toward the left wall.</summary>
    public static readonly Offset Left = new(0, -1);

    /// <summary>One column toward the right wall.</summary>
    public static readonly Offset Right = new(0, 1);
}
