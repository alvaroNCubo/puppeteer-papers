using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// A falling tetromino: a <see cref="Shape"/> of exactly four cells, with a
/// <see cref="PieceType"/> identity and an <see cref="Orientation"/>. This is
/// the abstract spine of the seven concrete pieces (<see cref="IPiece"/>,
/// <see cref="OPiece"/>, …). A subclass supplies one thing — the
/// <em>local</em> layout of its four cells for a given pose — and the base
/// class handles everything else: anchoring into the well, exposing world
/// cells, rotating, translating, and guarding the four-cell invariant.
/// <para>
/// Every transformation (<see cref="Translate"/>, <see cref="Rotate"/>)
/// returns a new piece; a piece never mutates. The well decides whether a
/// candidate piece is legal, never the piece itself — a piece has no notion of
/// walls or pile, only of its own shape.
/// </para>
/// </summary>
internal abstract class Piece : Shape
{
    /// <summary>The number of cells every tetromino has, by definition.</summary>
    public const int CellCount = 4;

    /// <summary>
    /// The top-left corner of the piece's bounding box in well coordinates.
    /// The subclass's local layout is expressed relative to this anchor, so
    /// translating the piece is simply translating the anchor.
    /// </summary>
    public Position Anchor { get; }

    /// <summary>This piece's current rotation pose.</summary>
    public Orientation Orientation { get; }

    /// <summary>Which of the seven tetrominoes this is.</summary>
    public abstract PieceType Type { get; }

    private readonly ImmutableHashSet<Position> _cells;

    /// <summary>
    /// Builds a piece at <paramref name="anchor"/> in pose
    /// <paramref name="orientation"/>, materialising and validating its four
    /// world cells.
    /// </summary>
    protected Piece(Position anchor, Orientation orientation)
    {
        if (orientation.DistinctCount != DistinctOrientations)
        {
            // The pose was minted for a piece with a different symmetry.
            throw new TetrisRuleException(
                $"{Type} has {DistinctOrientations} distinct orientations, but the pose declares {orientation.DistinctCount}.");
        }

        Anchor = anchor;
        Orientation = orientation;
        _cells = LocalCells(orientation.Index)
            .Select(local => anchor.Translate(new Offset(local.Row, local.Column)))
            .ToImmutableHashSet();

        if (_cells.Count != CellCount)
        {
            // Either the layout did not declare four cells, or two cells
            // coincided. Both break the defining invariant of a tetromino.
            throw new TetrisRuleException(
                $"{Type} in pose {orientation} occupies {_cells.Count} cells; a tetromino has exactly {CellCount}.");
        }
    }

    /// <inheritdoc />
    public override ImmutableHashSet<Position> Cells => _cells;

    /// <summary>
    /// The four cells of this piece, in pose <paramref name="orientationIndex"/>,
    /// expressed relative to <see cref="Anchor"/> (anchor-local coordinates).
    /// This is the single point of variation across the seven pieces — the
    /// place polymorphism does its work.
    /// </summary>
    protected abstract Position[] LocalCells(int orientationIndex);

    /// <summary>How many distinct poses this piece has (1, 2, or 4).</summary>
    protected abstract int DistinctOrientations { get; }

    /// <summary>This piece moved by <paramref name="offset"/> — same pose, new anchor.</summary>
    public Piece Translate(Offset offset) =>
        Rebuild(Anchor.Translate(offset), Orientation);

    /// <summary>
    /// This piece turned one quarter-turn — same anchor, next pose in the single
    /// rotation sense. Returns a new piece; the original is unchanged. For the
    /// square this is a no-op (one pose), with no effect and no error.
    /// </summary>
    public Piece Rotate() => Rebuild(Anchor, Orientation.Next());

    /// <summary>Reconstructs a piece of the concrete subtype with new state.</summary>
    protected abstract Piece Rebuild(Position anchor, Orientation orientation);

    public override string ToString() => $"{Type} @ {Anchor} [{Orientation}]";
}
