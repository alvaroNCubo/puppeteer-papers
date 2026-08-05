using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// A figure in the grid — something that answers, for any cell, "do you occupy
/// this?". A falling <see cref="Piece"/>, the accumulated <see cref="Pile"/>,
/// and the boundary <see cref="Frame"/> are all the same kind of thing:
/// <em>figures within figures</em>. Because they share this spine, "is this
/// placement legal?" is one question asked the same way of every figure — see
/// <see cref="Intersects"/>.
/// <para>
/// Membership is the primitive. <see cref="Occupies"/> is the only thing a
/// figure must be able to answer; <see cref="Cells"/> enumerates the occupied
/// cells for figures that have a finite, materialised extent (a piece, the
/// pile). A figure whose extent is more naturally a rule than a set — the
/// boundary — overrides <see cref="Occupies"/> as a predicate and need not
/// enumerate anything.
/// </para>
/// <para>
/// A shape is immutable: its cells never change, and transformations return new
/// shapes. The grid coordinates are abstract — a shape knows nothing of scores,
/// timers, or rendering.
/// </para>
/// </summary>
internal abstract class Shape
{
    /// <summary>
    /// The cells this shape occupies. For a piece this is its four cells; for
    /// the pile, every landed block. A predicate-defined figure (the boundary)
    /// may have an unbounded extent and is probed through <see cref="Occupies"/>
    /// rather than enumerated here.
    /// </summary>
    public abstract ImmutableHashSet<Position> Cells { get; }

    /// <summary>
    /// True when this figure occupies <paramref name="position"/>. The default
    /// is membership in <see cref="Cells"/>; figures with a closed-form extent
    /// override this with a predicate.
    /// </summary>
    public virtual bool Occupies(Position position) => Cells.Contains(position);

    /// <summary>
    /// True when this figure and <paramref name="other"/> share at least one
    /// cell — <em>the</em> collision primitive. It iterates <em>this</em>
    /// figure's cells and probes the other's membership, so it costs one
    /// <see cref="Occupies"/> call per cell of <c>this</c>.
    /// <para>
    /// Convention: always call it as <c>small.Intersects(large)</c>. In
    /// collision the four-cell piece is the small one
    /// (<c>piece.Intersects(frame)</c>, <c>piece.Intersects(pile)</c>); for
    /// game-over the small spawn region is the caller. That way the boundary —
    /// which has no finite cell set — is only ever the probed party, never the
    /// iterated one, and each collision costs O(cells of the small figure).
    /// </para>
    /// </summary>
    public bool Intersects(Shape other) => Cells.Any(other.Occupies);

    /// <summary>The set of cells obtained by translating every cell by the offset.</summary>
    protected ImmutableHashSet<Position> TranslatedCells(Offset offset) =>
        Cells.Select(cell => cell.Translate(offset)).ToImmutableHashSet();
}
