using System.Collections.Immutable;

namespace Tetris.Hex;

/// <summary>
/// The boundary of the well — its two walls and its floor — as a
/// <see cref="Shape"/> defined by a <em>predicate</em> rather than a
/// materialised set of cells. This is the answer to "is the frontier just
/// another figure?": <b>yes</b> — a figure exactly like a piece or the pile,
/// which is what collapses three apparent collision rules into one. A piece
/// bumping the left wall, hitting the floor, or landing on the pile are all the
/// same event: the piece's cells overlap some occupied figure.
/// <para>
/// The boundary's extent is unbounded (the walls run as high as a piece can
/// sit), so it is not enumerated. It answers <see cref="Occupies"/> in closed
/// form, and collision only ever probes it — the four-cell piece is the figure
/// that iterates, the boundary the one that is asked. <see cref="Cells"/> is
/// therefore unsupported.
/// </para>
/// <para>
/// The well is read like text: rows grow downward, columns rightward. The
/// interior playing field is rows <c>[0, Height)</c> × columns
/// <c>[0, Width)</c>. The left wall is every column below 0, the right wall
/// every column at or beyond <see cref="Width"/>, and the floor every row at or
/// beyond <see cref="Height"/>. The ceiling is open: a row above 0 is not
/// boundary, so pieces may sit above the top while spawning and falling in.
/// </para>
/// </summary>
internal sealed class Frame : Shape
{
    /// <summary>Number of interior columns (the playable width).</summary>
    public int Width { get; }

    /// <summary>Number of interior rows (the playable height).</summary>
    public int Height { get; }

    public Frame(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(width), "A well needs a positive interior.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>
    /// The boundary is predicate-defined and unbounded; it has no finite cell
    /// set. Probe it with <see cref="Occupies"/> instead.
    /// </summary>
    public override ImmutableHashSet<Position> Cells =>
        throw new System.NotSupportedException(
            "The frame is a boundary predicate, not a materialised set; use Occupies.");

    /// <summary>
    /// True when <paramref name="position"/> is part of the boundary: a wall
    /// column (left of 0 or at/beyond the width) or the floor (at/beyond the
    /// height). The top is open, so a row above 0 is never boundary.
    /// </summary>
    public override bool Occupies(Position position) =>
        position.Column < 0
        || position.Column >= Width
        || position.Row >= Height;

    /// <summary>
    /// True when <paramref name="position"/> lies in the interior — within the
    /// column range and above the floor. Rows above 0 (the open sky a piece
    /// spawns in) count as interior. This is the complement of the boundary
    /// among reachable cells, used for clipping the rendered view.
    /// </summary>
    public bool Contains(Position position) =>
        position.Column >= 0
        && position.Column < Width
        && position.Row < Height;
}
