namespace Tetris;

/// <summary>
/// The seven tetrominoes, named by the letter their silhouette resembles.
/// The type is the piece's identity; its geometry lives in the matching
/// <see cref="Piece"/> subclass, so adding a (hypothetical) eighth piece is a
/// new subclass and a new enum member, nothing else.
/// </summary>
internal enum PieceType
{
    /// <summary>Four cells in a line. Two distinct orientations.</summary>
    I,

    /// <summary>The 2x2 square. One orientation — rotation is a no-op.</summary>
    O,

    /// <summary>The tee. Four distinct orientations.</summary>
    T,

    /// <summary>The right-handed skew. Two distinct orientations.</summary>
    S,

    /// <summary>The left-handed skew. Two distinct orientations.</summary>
    Z,

    /// <summary>The blue ell mirrored. Four distinct orientations.</summary>
    J,

    /// <summary>The orange ell. Four distinct orientations.</summary>
    L,
}
