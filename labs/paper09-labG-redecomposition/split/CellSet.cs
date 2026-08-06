using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// A bare set of cells as a <see cref="Shape"/> — a figure with no behaviour of
/// its own, only an extent. It exists so that a set of cells that arrived from
/// somewhere else (the pile as projected into the piece role, the four cells of a
/// landed piece as told to the pile role) can be probed by the one collision
/// primitive, <see cref="Shape.Intersects"/>, exactly like a piece or the pile.
/// <para>
/// <see cref="Well"/> keeps its own private equivalent for the spawn region; this
/// one is the shared figure the two re-cut roles use. It is deliberately NOT
/// factored out of the well: the original stays untouched.
/// </para>
/// </summary>
internal sealed class CellSet : Shape
{
    /// <summary>The empty figure — occupies nothing, intersects nothing.</summary>
    internal static readonly CellSet Empty = new(ImmutableHashSet<Position>.Empty);

    internal CellSet(ImmutableHashSet<Position> cells) => Cells = cells;

    /// <inheritdoc />
    public override ImmutableHashSet<Position> Cells { get; }

    /// <summary>The figure occupying exactly <paramref name="cells"/>.</summary>
    internal static CellSet Of(IEnumerable<Position> cells) =>
        new(cells.ToImmutableHashSet());
}
