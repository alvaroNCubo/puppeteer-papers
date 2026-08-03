using System.Collections.Generic;

namespace Tetris.Hex.Tests;

/// <summary>
/// TEST DOUBLE #1 — a stand-in for the driven port <see cref="IBoardOutputPort"/>.
/// A spy: it records every view the hexagon presents so a test can assert on
/// what the application emitted.
/// <para>
/// This file exists because the hexagon declares the port. <see cref="GameService"/>
/// cannot be constructed without an <see cref="IBoardOutputPort"/>, so no test of
/// the application can run without this type or something like it — which is the
/// count the baseline was built to measure.
/// </para>
/// </summary>
internal sealed class RecordingBoardOutput : IBoardOutputPort
{
    private readonly List<BoardView> presented = [];

    /// <summary>Every view presented, in order.</summary>
    public IReadOnlyList<BoardView> Presented => presented;

    /// <summary>The most recent view presented.</summary>
    public BoardView Last => presented[^1];

    /// <summary>How many times the application presented a board.</summary>
    public int Count => presented.Count;

    public void Present(BoardView board) => presented.Add(board);
}
