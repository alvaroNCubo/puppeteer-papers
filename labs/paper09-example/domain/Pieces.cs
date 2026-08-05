namespace Tetris;

// The seven tetrominoes. Each is a small, closed statement of one piece's
// geometry: its identity, how many distinct poses it has, and the four cells
// of each pose in anchor-local coordinates (row down, column right). All the
// shared behaviour — anchoring, world cells, rotation cycling, translation,
// the four-cell invariant — lives once in the abstract base Piece. A subclass
// adds geometry, never mechanism. This is the polymorphism the model leans on:
// the well, the collision rule and the line clear never ask "which piece is
// this?" — they speak only Shape and Piece.

/// <summary>The bar. Two poses: horizontal and vertical.</summary>
internal sealed class IPiece : Piece
{
    public IPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.I;
    protected override int DistinctOrientations => 2;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(1, 0), new(1, 1), new(1, 2), new(1, 3)],
        _ => [new(0, 2), new(1, 2), new(2, 2), new(3, 2)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new IPiece(anchor, orientation);
}

/// <summary>The square. One pose: rotation leaves it unchanged.</summary>
internal sealed class OPiece : Piece
{
    public OPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.O;
    protected override int DistinctOrientations => 1;

    protected override Position[] LocalCells(int orientationIndex) =>
        [new(0, 0), new(0, 1), new(1, 0), new(1, 1)];

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new OPiece(anchor, orientation);
}

/// <summary>The tee. Four poses: point up, right, down, left.</summary>
internal sealed class TPiece : Piece
{
    public TPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.T;
    protected override int DistinctOrientations => 4;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(0, 1), new(1, 0), new(1, 1), new(1, 2)],
        1 => [new(0, 1), new(1, 1), new(1, 2), new(2, 1)],
        2 => [new(1, 0), new(1, 1), new(1, 2), new(2, 1)],
        _ => [new(0, 1), new(1, 0), new(1, 1), new(2, 1)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new TPiece(anchor, orientation);
}

/// <summary>The right-handed skew. Two poses.</summary>
internal sealed class SPiece : Piece
{
    public SPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.S;
    protected override int DistinctOrientations => 2;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(0, 1), new(0, 2), new(1, 0), new(1, 1)],
        _ => [new(0, 1), new(1, 1), new(1, 2), new(2, 2)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new SPiece(anchor, orientation);
}

/// <summary>The left-handed skew. Two poses.</summary>
internal sealed class ZPiece : Piece
{
    public ZPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.Z;
    protected override int DistinctOrientations => 2;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(0, 0), new(0, 1), new(1, 1), new(1, 2)],
        _ => [new(0, 2), new(1, 1), new(1, 2), new(2, 1)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new ZPiece(anchor, orientation);
}

/// <summary>The blue ell. Four poses.</summary>
internal sealed class JPiece : Piece
{
    public JPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.J;
    protected override int DistinctOrientations => 4;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(0, 0), new(1, 0), new(1, 1), new(1, 2)],
        1 => [new(0, 1), new(0, 2), new(1, 1), new(2, 1)],
        2 => [new(1, 0), new(1, 1), new(1, 2), new(2, 2)],
        _ => [new(0, 1), new(1, 1), new(2, 0), new(2, 1)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new JPiece(anchor, orientation);
}

/// <summary>The orange ell. Four poses.</summary>
internal sealed class LPiece : Piece
{
    public LPiece(Position anchor, Orientation orientation) : base(anchor, orientation) { }

    public override PieceType Type => PieceType.L;
    protected override int DistinctOrientations => 4;

    protected override Position[] LocalCells(int orientationIndex) => orientationIndex switch
    {
        0 => [new(0, 2), new(1, 0), new(1, 1), new(1, 2)],
        1 => [new(0, 1), new(1, 1), new(2, 1), new(2, 2)],
        2 => [new(1, 0), new(1, 1), new(1, 2), new(2, 0)],
        _ => [new(0, 0), new(0, 1), new(1, 1), new(2, 1)],
    };

    protected override Piece Rebuild(Position anchor, Orientation orientation) =>
        new LPiece(anchor, orientation);
}
