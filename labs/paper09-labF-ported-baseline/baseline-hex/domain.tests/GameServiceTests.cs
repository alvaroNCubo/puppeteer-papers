using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Hex.Tests;

/// <summary>
/// The hexagon tested the way ports and adapters prescribes: drive the
/// application through its DRIVING port and assert on what it pushed out of its
/// DRIVEN ports, with a stand-in for each driven port in place of a real adapter.
/// <para>
/// Every test in this class needs both doubles. That is not a stylistic choice
/// — <see cref="GameService"/> takes an <see cref="IBoardOutputPort"/> and an
/// <see cref="IPieceSelectionPort"/> in its constructor, so there is no way to
/// obtain one without supplying something for each, and no way to observe what
/// it did without the first. The model tests in <c>model/</c> need neither,
/// because the model declares no port; these need both, because the hexagon
/// does. The contrast is the measurement.
/// </para>
/// </summary>
[TestClass]
public sealed class GameServiceTests
{
    /// <summary>A hexagon wired to the two doubles — the only way to get one.</summary>
    private static (IGameCommandPort game, RecordingBoardOutput screen) Open(
        int width, int height, params string[] pieces)
    {
        var screen = new RecordingBoardOutput();
        var chooser = new ScriptedPieceSelection(pieces);
        return (new GameService(width, height, screen, chooser), screen);
    }

    private static string Cells(BoardView board) =>
        string.Join("|", board.Occupied.Select(c => $"{c.Row},{c.Column}"));

    [TestMethod]
    public void TheApplication_CannotBeConstructedWithoutAStandInForEachDrivenPort()
    {
        // The count this baseline exists to measure, as an executable statement:
        // the hexagon's entry point is uninstantiable with either driven port
        // absent, so a test of it supplies one double per driven side.
        Assert.ThrowsException<ArgumentNullException>(
            () => new GameService(10, 20, null!, ScriptedPieceSelection.Always("O")));
        Assert.ThrowsException<ArgumentNullException>(
            () => new GameService(10, 20, new RecordingBoardOutput(), null!));
    }

    [TestMethod]
    public void Start_ResolvesTheFirstPieceThroughThePort_AndPresentsTheOpeningBoard()
    {
        var (game, screen) = Open(10, 20, "T");

        game.Start();

        Assert.AreEqual(1, screen.Count, "starting presents exactly one board");
        var board = screen.Last;
        Assert.AreEqual(10, board.Width);
        Assert.AreEqual(20, board.Height);
        Assert.AreEqual("T", board.ActiveType, "the letter came from the selection port");
        Assert.IsFalse(board.IsAwaitingPiece, "a piece is falling");
        Assert.IsFalse(board.IsGameOver);
        Assert.AreEqual(0, board.ClearedLines);
        // T spawn pose at anchor (0,3): (0,4)(1,3)(1,4)(1,5)
        Assert.AreEqual("0,4|1,3|1,4|1,5", Cells(board));
    }

    [TestMethod]
    public void EveryVerb_BeforeStart_IsAGentleNoOp_AndPresentsNothing()
    {
        var (game, screen) = Open(10, 20, "O");

        game.MoveLeft();
        game.MoveRight();
        game.Rotate();
        game.Tick();
        game.Drop();

        Assert.AreEqual(0, screen.Count, "no piece is falling, so nothing happened at all");
    }

    [TestMethod]
    public void MoveRight_PresentsTheShiftedBoard()
    {
        // Width 4: an O spawns at anchor (0,0), columns 0..1.
        var (game, screen) = Open(4, 10, "O");
        game.Start();

        game.MoveRight();

        Assert.AreEqual(2, screen.Count);
        Assert.AreEqual("0,1|0,2|1,1|1,2", Cells(screen.Last));
    }

    [TestMethod]
    public void MoveLeft_BlockedByTheWall_StillPresents_ButTheBoardIsUnchanged()
    {
        // The O spawns against the left wall, so the slide is a legal no-op.
        var (game, screen) = Open(4, 10, "O");
        game.Start();
        var before = Cells(screen.Last);

        game.MoveLeft();

        Assert.AreEqual(2, screen.Count, "a blocked move is still an operation");
        Assert.AreEqual(before, Cells(screen.Last), "and it moved nothing");
    }

    [TestMethod]
    public void Rotate_CyclesThePose_AsPresented()
    {
        // Width 4, height 8: the I spawns horizontal across row 1, and turning it
        // vertical into open space is accepted.
        var (game, screen) = Open(4, 8, "I");
        game.Start();
        Assert.AreEqual("1,0|1,1|1,2|1,3", Cells(screen.Last), "spawns horizontal");

        game.Rotate();

        Assert.AreEqual("0,2|1,2|2,2|3,2", Cells(screen.Last), "presented vertical");
    }

    [TestMethod]
    public void Drop_LandsThePiece_AndFeedsTheNextOneBeforePresenting()
    {
        // Width 4, height 6. The first O rests on the floor at rows 4..5; the
        // application feeds the second piece, so the presented board never shows
        // a well between pieces.
        var (game, screen) = Open(4, 6, "O", "T");
        game.Start();

        game.Drop();

        Assert.AreEqual(2, screen.Count, "one present per operation, landing included");
        var board = screen.Last;
        Assert.IsFalse(board.IsAwaitingPiece, "the next piece was fed before presenting");
        Assert.AreEqual("T", board.ActiveType, "and it came from the selection port");
        // The landed O (rows 4..5, cols 0..1) plus the T's spawn pose at (0,0).
        Assert.AreEqual("0,1|1,0|1,1|1,2|4,0|4,1|5,0|5,1", Cells(board));
    }

    [TestMethod]
    public void Tick_DescendsARowPerCall_AndLandsAtTheFloor()
    {
        // Width 4, height 6. The O spawns at rows 0..1 and the floor is row 6, so
        // four ticks bring it to rows 4..5 and the fifth lands it.
        var (game, screen) = Open(4, 6, "O", "O");
        game.Start();

        for (var i = 0; i < 4; i++)
        {
            game.Tick();
        }

        Assert.AreEqual("4,0|4,1|5,0|5,1", Cells(screen.Last), "resting on the floor line");

        game.Tick(); // lands, and the next piece is fed

        Assert.AreEqual("0,0|0,1|1,0|1,1|4,0|4,1|5,0|5,1", Cells(screen.Last));
        Assert.IsFalse(screen.Last.IsAwaitingPiece);
    }

    [TestMethod]
    public void Drop_CompletingARow_PresentsTheClearedCount()
    {
        // Width 4: a horizontal I fills the whole width of its row, so it clears
        // as it lands.
        var (game, screen) = Open(4, 6, "I", "I");
        game.Start();

        game.Drop();

        Assert.AreEqual(1, screen.Last.ClearedLines, "the full floor row cleared");
        Assert.AreEqual("1,0|1,1|1,2|1,3", Cells(screen.Last), "only the next I remains");
    }

    [TestMethod]
    public void GameOver_IsPresented_WhenThePileRisesIntoTheSpawnRegion()
    {
        // Width 4, height 2 — the spawn region is the whole interior, so the first
        // landing ends the game.
        var (game, screen) = Open(4, 2, "O", "O");
        game.Start();
        Assert.IsFalse(screen.Last.IsGameOver, "play opens normally");

        game.Drop();

        var board = screen.Last;
        Assert.IsTrue(board.IsGameOver, "the pile reached the spawn region");
        Assert.IsFalse(board.IsAwaitingPiece, "game over is not 'awaiting a piece'");
        Assert.IsNull(board.ActiveType, "no piece is falling once the game is over");
    }

    [TestMethod]
    public void EveryVerb_AfterGameOver_IsAGentleNoOp_AndPresentsNothingFurther()
    {
        var (game, screen) = Open(4, 2, "O", "O");
        game.Start();
        game.Drop(); // reach game over
        var presentsSoFar = screen.Count;

        game.MoveLeft();
        game.MoveRight();
        game.Rotate();
        game.Tick();
        game.Drop();

        Assert.AreEqual(presentsSoFar, screen.Count, "a finished game accepts nothing");
        Assert.IsTrue(screen.Last.IsGameOver);
    }

    [TestMethod]
    public void ALetterThePortInventsThatNamesNoTetromino_IsRejected()
    {
        // The port is a contract the hexagon depends on, so the hexagon has to
        // defend itself against an adapter that breaks it.
        var (game, _) = Open(10, 20, "Q");

        Assert.ThrowsException<TetrisRuleException>(() => game.Start());
    }

    [TestMethod]
    public void TheSameScriptedStream_PresentsTheSameBoard_Twice()
    {
        // Determinism of the application, given a deterministic selection port —
        // the port-level counterpart of DeterminismTests over the model.
        static string Play()
        {
            var (game, screen) = Open(8, 16, "T", "I", "O", "S", "Z", "J", "L");
            game.Start();
            game.MoveLeft(); game.Rotate(); game.Drop();
            game.Rotate(); game.MoveRight(); game.Drop();
            game.MoveLeft(); game.MoveLeft(); game.Drop();
            game.Tick(); game.MoveRight(); game.Drop();
            return $"cleared={screen.Last.ClearedLines};type={screen.Last.ActiveType};{Cells(screen.Last)}";
        }

        Assert.AreEqual(Play(), Play(), "two runs of one command stream must match exactly");
    }
}
