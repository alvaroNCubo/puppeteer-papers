using System.Collections.Generic;

namespace Tetris.Acting;

/// <summary>
/// An immutable, framework-free view of the well at one moment — everything a
/// host needs to render a frame and make its query-first control-flow decisions.
/// The <see cref="TetrisActor"/> produces it by running a single query over the
/// actor and parsing the result; nothing of the engine (no DSL, no Performance)
/// leaks past this type.
/// </summary>
public sealed record WellSnapshot(
    int Width,
    int Height,
    IReadOnlyList<Cell> Occupied,
    IReadOnlyList<Cell> Active,
    int ClearedLines,
    bool IsGameOver,
    bool IsAwaitingPiece,
    string? ActiveType = null);

/// <summary>A single occupied grid cell (row grows downward, column rightward).</summary>
public readonly record struct Cell(int Row, int Column);
