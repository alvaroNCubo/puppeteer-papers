using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Hex.Tests;

/// <summary>
/// The hexagon as staging 4 uses it: reopened from a state port on every
/// operation, the way a client that runs one operation per process must have it.
/// <para>
/// Every test here needs THREE doubles — the board output port to observe, the
/// piece-selection port to be deterministic, and now the state port, without
/// which this constructor cannot be called at all. That is the count this
/// increment moved.
/// </para>
/// </summary>
[TestClass]
public sealed class PersistedGameServiceTests
{
    private const string Session = "test-session";

    private static (IGameCommandPort game, RecordingBoardOutput screen) Reopen(
        InMemoryGameState store, int width, int height, params string[] pieces) =>
        Reopen(store, width, height, new RecordingBoardOutput(), pieces);

    private static (IGameCommandPort game, RecordingBoardOutput screen) Reopen(
        InMemoryGameState store, int width, int height, RecordingBoardOutput screen, params string[] pieces)
    {
        var game = new GameService(Session, width, height, screen, new ScriptedPieceSelection(pieces), store);
        return (game, screen);
    }

    private static string Cells(BoardView board) =>
        string.Join("|", board.Occupied.Select(c => $"{c.Row},{c.Column}"));

    [TestMethod]
    public void ThePersistentConstructor_CannotBeCalledWithoutAStandInForEveryDrivenPort()
    {
        var screen = new RecordingBoardOutput();
        var pieces = ScriptedPieceSelection.Always("O");
        var store = new InMemoryGameState();

        Assert.ThrowsException<ArgumentNullException>(
            () => new GameService(Session, 10, 20, null!, pieces, store));
        Assert.ThrowsException<ArgumentNullException>(
            () => new GameService(Session, 10, 20, screen, null!, store));
        Assert.ThrowsException<ArgumentNullException>(
            () => new GameService(Session, 10, 20, screen, pieces, null!));
    }

    [TestMethod]
    public void AFreshSession_OpensANewWell_AndRecordsOncePerOperation()
    {
        var store = new InMemoryGameState();
        var (game, screen) = Reopen(store, 10, 20, "T");

        Assert.AreEqual(1, store.Loads, "the application asked the port for saved state");
        Assert.AreEqual(0, store.Saves, "and nothing was recorded before the first operation");

        game.Start();

        Assert.AreEqual(1, store.Saves, "starting recorded the opening position");
        Assert.AreEqual("T", screen.Last.ActiveType);
    }

    [TestMethod]
    public void ASavedGame_ReopensExactlyWhereItWasLeft()
    {
        var store = new InMemoryGameState();

        // Process 1: open, drop the O onto the floor, and let the next piece in.
        var (first, firstScreen) = Reopen(store, 4, 6, "O", "T");
        first.Start();
        first.Drop();
        var leftAt = Cells(firstScreen.Last);
        var typeAt = firstScreen.Last.ActiveType;

        // Process 2: a brand-new service over the same store presents the same board.
        var (second, secondScreen) = Reopen(store, 4, 6, "Z");
        second.Show();

        Assert.AreEqual(leftAt, Cells(secondScreen.Last), "the board resumed cell for cell");
        Assert.AreEqual(typeAt, secondScreen.Last.ActiveType, "including which piece was falling");
        Assert.AreEqual(0, secondScreen.Last.ClearedLines);
    }

    [TestMethod]
    public void AReopenedGame_ResumesTheFallingPieceInItsRecordedPose()
    {
        var store = new InMemoryGameState();

        // Turn the I vertical, then hand the session over.
        var (first, firstScreen) = Reopen(store, 4, 8, "I");
        first.Start();
        first.Rotate();
        Assert.AreEqual("0,2|1,2|2,2|3,2", Cells(firstScreen.Last), "vertical before the handover");

        var (second, secondScreen) = Reopen(store, 4, 8, "I");
        second.Show();

        Assert.AreEqual("0,2|1,2|2,2|3,2", Cells(secondScreen.Last), "still vertical after it");
    }

    [TestMethod]
    public void OneOperationPerProcess_PlaysTheSameGameAsOneProcess()
    {
        // The point of the staging: a game driven one call at a time across many
        // services must reach the state a single service would have reached.
        // Three pieces are spawned over these five operations: the first by Start,
        // then one after each of the two landings.
        var script = new[] { "O", "O", "O" };

        // One process, holding the hexagon in memory throughout.
        var oneShotScreen = new RecordingBoardOutput();
        var single = new GameService(4, 6, oneShotScreen, new ScriptedPieceSelection(script));
        single.Start();
        single.Drop();
        single.MoveRight();
        single.MoveRight();
        single.Drop();

        // The same five operations, each in a FRESH service over one store — the
        // shape a process-per-operation client has. The selection port is
        // process-local, so each step is given the letter that step will need
        // (Start and each landing consume one; a slide consumes none).
        var store = new InMemoryGameState();
        var steps = new (Action<IGameCommandPort> Op, string Needs)[]
        {
            (g => g.Start(), "O"),      // first piece
            (g => g.Drop(), "O"),       // lands, so the next piece is fed
            (g => g.MoveRight(), "-"),  // no spawn: the letter is never asked for
            (g => g.MoveRight(), "-"),
            (g => g.Drop(), "O"),       // clears two rows, feeding the third piece
        };

        RecordingBoardOutput? lastScreen = null;
        foreach (var (op, needs) in steps)
        {
            var (game, screen) = Reopen(store, 4, 6, needs);
            op(game);
            lastScreen = screen;
        }

        Assert.IsNotNull(lastScreen);
        Assert.AreEqual(
            Cells(oneShotScreen.Last),
            Cells(lastScreen!.Last),
            "one op per process reached the same board as one process");
        Assert.AreEqual(oneShotScreen.Last.ClearedLines, lastScreen!.Last.ClearedLines);
        Assert.AreEqual(oneShotScreen.Last.ActiveType, lastScreen!.Last.ActiveType);
    }

    [TestMethod]
    public void Show_PresentsTheBoard_AndRecordsNothing()
    {
        var store = new InMemoryGameState();
        var (game, screen) = Reopen(store, 10, 20, "T");
        game.Start();
        var savesAfterStart = store.Saves;

        game.Show();

        Assert.AreEqual(savesAfterStart, store.Saves, "showing the board is not an operation on it");
        Assert.AreEqual(2, screen.Count, "but it did present");
    }

    [TestMethod]
    public void SavedStateNamingAPieceThatIsNotATetromino_IsRejectedOnReopen()
    {
        // The state port is a contract like any other, so the hexagon defends
        // itself against an adapter that hands back nonsense.
        var store = new BadStateStub();

        Assert.ThrowsException<TetrisRuleException>(
            () => new GameService(Session, 10, 20, new RecordingBoardOutput(),
                ScriptedPieceSelection.Always("O"), store));
    }

    /// <summary>A fourth stand-in, for the one thing the fake cannot do: lie.</summary>
    private sealed class BadStateStub : IGameStatePort
    {
        public GameState? Load(string session) =>
            new(10, 20, 0, [], "Q", 0, 3, 0);

        public void Save(string session, GameState state)
        {
        }
    }
}
