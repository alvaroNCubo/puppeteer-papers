using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Tetris.Hex;

/// <summary>
/// The accumulated landed blocks — the floor that does not move but mutates.
/// A <see cref="Shape"/> like everything else, so the collision rule treats it
/// no differently from a wall. A pile is immutable: integrating a landed piece
/// returns a <em>new</em> pile.
/// <para>
/// The pile carries the well's <see cref="Width"/> because "complete" is
/// width-relative: a row is complete when every interior column in it is
/// occupied. The pile owns the whole landing transition — absorbing the piece
/// and collapsing any rows it completed — and enforces one invariant by
/// construction: <b>a pile never retains a complete row</b>. The row-completion
/// and collapse logic are private; the well asks only for <see cref="Integrate"/>.
/// </para>
/// </summary>
internal sealed class Pile : Shape
{
    /// <summary>The well's interior width; sets what "a complete row" means.</summary>
    public int Width { get; }

    private readonly ImmutableHashSet<Position> _cells;

    private Pile(int width, ImmutableHashSet<Position> cells)
    {
        Width = width;
        _cells = cells;
    }

    /// <summary>An empty pile for a well of the given interior width.</summary>
    public static Pile Empty(int width)
    {
        if (width <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(width));
        }

        return new Pile(width, ImmutableHashSet<Position>.Empty);
    }

    /// <summary>
    /// A pile holding exactly <paramref name="cells"/> — the seam a state port
    /// needs to put a saved pile back. Added for staging 4, because a client that
    /// runs one operation per process has to reload the board it left behind, and
    /// the only way in was through the model.
    /// </summary>
    internal static Pile Of(int width, ImmutableHashSet<Position> cells)
    {
        if (width <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(width));
        }

        return new Pile(width, cells);
    }

    /// <inheritdoc />
    public override ImmutableHashSet<Position> Cells => _cells;

    /// <summary>
    /// Absorbs a landed <paramref name="piece"/> and settles the pile in one
    /// operation: it merges the piece's cells, removes every row the piece
    /// completed (bottom-up, so blocks above a vanished line fall to fill it),
    /// and returns the new pile — which by construction holds no complete row —
    /// together with the indices of the rows that collapsed. The caller (the
    /// well) is responsible for having checked the piece actually rests here.
    /// </summary>
    public (Pile pile, IReadOnlyList<int> collapsedRows) Integrate(Piece piece)
    {
        var merged = new Pile(Width, _cells.Union(piece.Cells));
        var completed = merged.CompleteRows();
        if (completed.IsEmpty)
        {
            return (merged, System.Array.Empty<int>());
        }

        return (merged.ClearCompleteRows(completed), completed);
    }

    /// <summary>The row indices that are completely filled across the width.</summary>
    private ImmutableSortedSet<int> CompleteRows()
    {
        var occupiedByRow = _cells
            .GroupBy(cell => cell.Row)
            .Where(group => group.Select(cell => cell.Column).Distinct().Count() == Width)
            .Select(group => group.Key);

        return ImmutableSortedSet.CreateRange(occupiedByRow);
    }

    /// <summary>
    /// Removes the given complete rows and lets the blocks above each cleared
    /// row fall by the number of cleared rows beneath them — the bottom-up
    /// settling that makes a tower above a vanished line end one row lower.
    /// Returns a new pile that retains no complete row.
    /// </summary>
    private Pile ClearCompleteRows(ImmutableSortedSet<int> completed)
    {
        var survivors = ImmutableHashSet.CreateBuilder<Position>();
        foreach (var cell in _cells)
        {
            if (completed.Contains(cell.Row))
            {
                continue; // this cell was on a cleared line; it vanishes
            }

            // A surviving cell drops by one row for every cleared line strictly
            // below it. Processing the whole pile this way is the bottom-up
            // collapse: lines lower in the well pull everything above them down.
            var clearedBelow = completed.Count(clearedRow => clearedRow > cell.Row);
            survivors.Add(cell.Translate(new Offset(clearedBelow, 0)));
        }

        return new Pile(Width, survivors.ToImmutable());
    }

    /// <summary>
    /// Whether the pile still holds any complete row. Used by the well's
    /// invariant check; in valid play it is always false after a landing.
    /// </summary>
    internal bool HasCompleteRow() => !CompleteRows().IsEmpty;
}
