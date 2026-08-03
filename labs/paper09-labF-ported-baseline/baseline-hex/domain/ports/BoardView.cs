namespace Tetris.Hex;

using System.Collections.Generic;

/// <summary>
/// The outbound contract carried by <see cref="IBoardOutputPort"/> — an
/// immutable, adapter-agnostic view of the well at one moment. It is part of
/// the port, so it lives inside the hexagon: an adapter may read it but the
/// hexagon owns what it contains.
/// <para>
/// Deliberately the same field set as the journaled example's
/// <c>WellSnapshot</c> / pushed frame (width, height, occupied cells, cleared
/// count, the two state flags, the active piece's letter), so that the two
/// arrangements are compared on the same information and not on a richer or
/// poorer view.
/// </para>
/// </summary>
public sealed record BoardView(
    int Width,
    int Height,
    IReadOnlyList<BoardCell> Occupied,
    int ClearedLines,
    bool IsGameOver,
    bool IsAwaitingPiece,
    string? ActiveType);

/// <summary>One occupied grid cell (row grows downward, column rightward).</summary>
public readonly record struct BoardCell(int Row, int Column);
