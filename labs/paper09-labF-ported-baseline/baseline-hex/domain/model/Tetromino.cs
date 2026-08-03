namespace Tetris.Hex;

/// <summary>
/// The single place that knows how to turn a <see cref="PieceType"/> into a
/// concrete <see cref="Piece"/> at its spawn pose. Centralising this keeps the
/// rest of the model speaking <see cref="Piece"/> rather than branching on
/// type, and keeps each piece's distinct-orientation count next to its
/// geometry rather than scattered.
/// </summary>
internal static class Tetromino
{
    /// <summary>
    /// Creates the tetromino of the given <paramref name="type"/> at its spawn
    /// pose, with its bounding box anchored at <paramref name="anchor"/>.
    /// </summary>
    public static Piece Spawn(PieceType type, Position anchor) => type switch
    {
        PieceType.I => new IPiece(anchor, Orientation.Spawn(2)),
        PieceType.O => new OPiece(anchor, Orientation.Spawn(1)),
        PieceType.T => new TPiece(anchor, Orientation.Spawn(4)),
        PieceType.S => new SPiece(anchor, Orientation.Spawn(2)),
        PieceType.Z => new ZPiece(anchor, Orientation.Spawn(2)),
        PieceType.J => new JPiece(anchor, Orientation.Spawn(4)),
        PieceType.L => new LPiece(anchor, Orientation.Spawn(4)),
        _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, "Unknown tetromino."),
    };
}
