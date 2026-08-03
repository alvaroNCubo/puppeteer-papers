using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Hex.Tests;

/// <summary>
/// Behaviour of the aggregate root: inbound spawning, guarded moves, landing,
/// line clears, the three states (falling / between pieces / over), and the
/// query-first throw-vs-no-op contract. Pieces are supplied explicitly through
/// <see cref="Well.Spawn"/>, so every scenario is an exact, deterministic
/// command stream.
/// </summary>
[TestClass]
public sealed class WellTests
{
    /// <summary>Opens an empty well and spawns <paramref name="first"/> — the common starting point.</summary>
    private static Well OpenWith(int width, int height, PieceType first)
    {
        var well = new Well(width, height);
        well.Spawn(first);
        return well;
    }

    [TestMethod]
    public void Open_IsEmpty_AndAwaitingItsFirstPiece()
    {
        var well = new Well(10, 20);

        Assert.IsNull(well.Active, "no active piece before the first spawn");
        Assert.IsFalse(well.IsGameOver);
        Assert.IsTrue(well.IsAwaitingPiece, "an empty well awaits its first piece");
        Assert.AreEqual(0, well.Pile.Cells.Count);
    }

    [TestMethod]
    public void Spawn_PlacesThePieceCentred_AndStartsItFalling()
    {
        // Width 10 -> 4-wide box anchored at column (10-4)/2 = 3.
        var well = OpenWith(10, 20, PieceType.O);

        Assert.IsFalse(well.IsAwaitingPiece, "a piece is now falling");
        Assert.IsNotNull(well.Active);
        Assert.AreEqual(PieceType.O, well.Active!.Type);
        // O spawn cells: (0,3)(0,4)(1,3)(1,4)
        Assert.IsTrue(well.Active.Occupies(new Position(0, 3)));
        Assert.IsTrue(well.Active.Occupies(new Position(1, 4)));
    }

    [TestMethod]
    public void Spawn_WhileAPieceIsAlreadyFalling_Throws()
    {
        var well = OpenWith(10, 20, PieceType.O);

        Assert.ThrowsException<TetrisRuleException>(() => well.Spawn(PieceType.T));
        Assert.AreEqual(PieceType.O, well.Active!.Type, "the falling piece is untouched");
    }

    [TestMethod]
    public void MoveLeft_IsBlockedByTheWall_AndIsANoOpAtTheEdge()
    {
        // Width 4 -> O spawns at column 0, already against the left wall.
        var well = OpenWith(4, 10, PieceType.O);
        var before = well.Active!.Cells;

        well.MoveLeft(); // would push into column -1 (the wall): rejected, no throw

        Assert.IsTrue(well.Active!.Cells.SetEquals(before), "move into the wall is a no-op");
    }

    [TestMethod]
    public void MoveRight_SlidesUntilItMeetsTheWall_ThenStops()
    {
        var well = OpenWith(4, 10, PieceType.O);
        // O occupies columns 0..1; can move right once to columns 1..2,
        // and again to columns 2..3 (right wall at column 4). A third is blocked.
        well.MoveRight();
        well.MoveRight();
        Assert.IsTrue(well.Active!.Occupies(new Position(0, 2)));
        Assert.IsTrue(well.Active.Occupies(new Position(0, 3)));

        well.MoveRight(); // into the wall: rejected
        Assert.IsTrue(well.Active.Occupies(new Position(0, 3)), "still against the wall");
    }

    [TestMethod]
    public void Tick_DescendsUntilTheFloor_ThenLands_LeavingTheWellAwaitingTheNextPiece()
    {
        // Width 4, height 6. O spawns at rows 0..1. Floor is row 6.
        var well = OpenWith(4, 6, PieceType.O);

        well.Tick(); // -> rows 1..2
        well.Tick(); // -> rows 2..3
        well.Tick(); // -> rows 3..4
        well.Tick(); // -> rows 4..5
        Assert.IsTrue(well.Active!.Occupies(new Position(5, 0)), "resting on the floor line");

        well.Tick(); // lands; the well is now between pieces (no auto-spawn)
        Assert.IsTrue(well.Pile.Occupies(new Position(5, 0)), "landed cell joined the pile");
        Assert.IsTrue(well.Pile.Occupies(new Position(4, 1)));
        Assert.IsNull(well.Active, "landing does not spawn the next piece");
        Assert.IsTrue(well.IsAwaitingPiece, "the well awaits the next piece");

        well.Spawn(PieceType.O); // the host supplies the next piece
        Assert.AreEqual(PieceType.O, well.Active!.Type);
        Assert.IsTrue(well.Active.Occupies(new Position(0, 0)), "next piece spawned at the top");
    }

    [TestMethod]
    public void Drop_LandsAPieceOnTopOfThePile()
    {
        var well = OpenWith(4, 6, PieceType.O);

        well.Drop(); // first O rests on the floor: rows 4..5
        Assert.IsTrue(well.Pile.Occupies(new Position(5, 0)));
        Assert.IsTrue(well.Pile.Occupies(new Position(4, 1)));

        well.Spawn(PieceType.O);
        well.Drop(); // second O lands on top of the first: rows 2..3
        Assert.IsTrue(well.Pile.Occupies(new Position(3, 0)), "stacked on top of the pile");
        Assert.IsTrue(well.Pile.Occupies(new Position(2, 1)));
    }

    [TestMethod]
    public void Landing_IntegratesThePieceIntoThePile()
    {
        // Height 6: floor at row 6. An O falls until its bottom rests on row 5 —
        // landing cells rows 4..5.
        var well = OpenWith(4, 6, PieceType.O);

        well.Drop();

        foreach (var cell in new[]
                 {
                     new Position(4, 0), new Position(4, 1),
                     new Position(5, 0), new Position(5, 1),
                 })
        {
            Assert.IsTrue(well.Pile.Occupies(cell), $"pile should contain {cell}");
        }
    }

    [TestMethod]
    public void Rotate_IsAllowedInOpenSpace_AndCyclesThePose()
    {
        // Width 4, height 8. An I-piece spawns horizontal across columns 0..3
        // (row 1). Rotating sweeps column 2 across rows 0..3, which is open, so
        // the rotation is accepted; rotating again cycles it back (2 poses).
        var well = OpenWith(4, 8, PieceType.I);

        Assert.AreEqual(0, well.Active!.Orientation.Index, "spawns horizontal");
        well.Rotate();
        Assert.AreEqual(1, well.Active!.Orientation.Index, "rotates to vertical in open space");
        well.Rotate();
        Assert.AreEqual(0, well.Active!.Orientation.Index, "cycles back to horizontal");
    }

    [TestMethod]
    public void Rotate_IsRejectedWhenItWouldCollideWithTheWall()
    {
        // Width 4, height 8. The I-piece spawns horizontal (cols 0..3, row 1).
        // Turn it vertical (column 2), then shove it to the left wall. Turning
        // it again (back to horizontal) there would sweep columns -2..1 — across
        // the left wall — so the rotation must be rejected as a no-op.
        var well = OpenWith(4, 8, PieceType.I);

        well.Rotate();            // -> vertical, column 2
        well.MoveLeft();          // -> column 1
        well.MoveLeft();          // -> column 0 (against the left wall)
        Assert.IsTrue(well.Active!.Occupies(new Position(0, 0)), "vertical bar at the left wall");

        var before = well.Active!.Cells;
        well.Rotate(); // back to horizontal would cross the wall: rejected, no throw
        Assert.IsTrue(well.Active!.Cells.SetEquals(before), "rotation into the wall rejected");
        Assert.AreEqual(1, well.Active!.Orientation.Index, "still vertical");
    }

    [TestMethod]
    public void Rotate_IsRejectedWhenItWouldCollideWithThePile()
    {
        // Width 4, height 8. Build a two-cell-tall pile under the right half so
        // that an I-piece, rotated to vertical against it, would overlap.
        //
        // Drop an O shoved right (rests rows 6..7, cols 2..3). Tick a horizontal
        // I down to row 5; rotating to vertical would sweep column 2 across rows
        // 4..7 — rows 6,7 there are filled — so the rotation is rejected.
        var well = OpenWith(4, 8, PieceType.O);

        well.MoveRight();
        well.MoveRight();
        well.Drop(); // O rests at rows 6..7, cols 2..3

        Assert.IsTrue(well.Pile.Occupies(new Position(6, 2)));
        Assert.IsTrue(well.Pile.Occupies(new Position(7, 3)));

        well.Spawn(PieceType.I);
        for (var i = 0; i < 4; i++) // rows 1 -> 5
        {
            well.Tick();
        }
        Assert.AreEqual(PieceType.I, well.Active!.Type, "I has not landed yet");
        Assert.IsTrue(well.Active!.Occupies(new Position(5, 2)), "I rests at row 5");

        var beforeRotate = well.Active!.Cells;
        well.Rotate(); // vertical would be column 2 rows 4..7; rows 6,7 are filled
        Assert.IsTrue(well.Active!.Cells.SetEquals(beforeRotate), "rotation into the pile rejected");
        Assert.AreEqual(0, well.Active!.Orientation.Index, "still horizontal");
    }

    [TestMethod]
    public void Drop_ClearsACompleteRow()
    {
        // Width 4. A horizontal I fills all 4 columns of one row. Drop two to
        // clear the floor row twice.
        var well = OpenWith(4, 6, PieceType.I);

        well.Drop(); // I rests on the floor: horizontal across row 5 — a full row, clears at once
        Assert.AreEqual(1, well.ClearedLines, "the full floor row cleared");
        Assert.AreEqual(0, well.Pile.Cells.Count, "pile is empty after the clear");
        Assert.IsTrue(well.IsAwaitingPiece);

        well.Spawn(PieceType.I);
        well.Drop(); // second I again fills and clears the floor row
        Assert.AreEqual(2, well.ClearedLines);
        Assert.AreEqual(0, well.Pile.Cells.Count);
    }

    [TestMethod]
    public void LineClear_DropsAStackedBlockLower()
    {
        // Width 4, height 6. Survivor scenario:
        //   - O #1 -> floor left  (cols 0..1, rows 4..5)
        //   - O #2 stacked on #1  (cols 0..1, rows 2..3)
        //   - O #3 -> floor right (cols 2..3, rows 4..5) completes rows 4 and 5
        // Rows 4 and 5 clear; the surviving block (rows 2..3, cols 0..1) drops by
        // two -> rows 4..5.
        var well = OpenWith(4, 6, PieceType.O);

        well.Drop();                 // O#1 -> rows 4..5, cols 0..1
        well.Spawn(PieceType.O);
        well.Drop();                 // O#2 -> rows 2..3, cols 0..1 (on top of #1)
        Assert.AreEqual(0, well.ClearedLines, "nothing full yet");

        well.Spawn(PieceType.O);
        well.MoveRight();            // shift O#3 right: cols 0..1 -> 1..2
        well.MoveRight();            // -> cols 2..3
        well.Drop();                 // O#3 -> rows 4..5, cols 2..3 => rows 4 and 5 full

        Assert.AreEqual(2, well.ClearedLines, "two rows cleared at once");
        // The survivor block (was rows 2..3, cols 0..1) dropped by two rows.
        Assert.IsTrue(well.Pile.Occupies(new Position(4, 0)));
        Assert.IsTrue(well.Pile.Occupies(new Position(5, 1)));
        Assert.AreEqual(4, well.Pile.Cells.Count, "only the 2x2 survivor remains");
    }

    [TestMethod]
    public void IsGameOver_FlipsWhenThePileRisesIntoTheSpawnRegion()
    {
        // Width 4, height 2 — a shallow well. The spawn region is rows 0..1,
        // columns 0..3. An O dropped onto the floor (rows 0..1, cols 0..1) lands
        // straight into the spawn region, so the derived game-over flips.
        var well = OpenWith(4, 2, PieceType.O);

        Assert.IsFalse(well.IsGameOver, "play opens normally");
        Assert.IsNotNull(well.Active);

        well.Drop(); // O fills rows 0..1, cols 0..1 — inside the spawn region

        Assert.IsTrue(well.IsGameOver, "pile has risen into the spawn region");
        Assert.IsFalse(well.IsAwaitingPiece, "game over is not 'awaiting a piece'");
        Assert.IsNull(well.Active, "no active piece once the game is over");
    }

    [TestMethod]
    public void Spawn_OnAFinishedGame_Throws()
    {
        var well = OpenWith(4, 2, PieceType.O);
        well.Drop(); // reach game over
        Assert.IsTrue(well.IsGameOver);

        Assert.ThrowsException<TetrisRuleException>(() => well.Spawn(PieceType.O));
    }

    [TestMethod]
    public void EveryMoveVerb_ThrowsWhenAwaitingAPiece_AndLeavesTheWellUnchanged()
    {
        // Between pieces: there is no active piece, so the move verbs are invalid.
        var well = OpenWith(4, 6, PieceType.O);
        well.Drop(); // lands -> between pieces
        Assert.IsTrue(well.IsAwaitingPiece);

        AssertMoveVerbsThrowAndStateUnchanged(well);
    }

    [TestMethod]
    public void EveryMoveVerb_ThrowsOnAFinishedGame_AndLeavesTheWellUnchanged()
    {
        var well = OpenWith(4, 2, PieceType.O);
        well.Drop(); // reach game over
        Assert.IsTrue(well.IsGameOver);

        AssertMoveVerbsThrowAndStateUnchanged(well);
    }

    private static void AssertMoveVerbsThrowAndStateUnchanged(Well well)
    {
        var pileBefore = well.Pile.Cells;
        var clearedBefore = well.ClearedLines;
        var gameOverBefore = well.IsGameOver;

        foreach (System.Action verb in new System.Action[]
                 {
                     well.MoveLeft, well.MoveRight, well.Rotate, well.Tick, well.Drop,
                 })
        {
            Assert.ThrowsException<TetrisRuleException>(() => verb());

            Assert.IsNull(well.Active, "still no active piece");
            Assert.AreEqual(gameOverBefore, well.IsGameOver, "game-over state unchanged");
            Assert.IsTrue(well.Pile.Cells.SetEquals(pileBefore), "pile unchanged by the failed verb");
            Assert.AreEqual(clearedBefore, well.ClearedLines, "cleared count unchanged by the failed verb");
        }
    }

    [TestMethod]
    public void BlockedMove_IsANoOp_NotAThrow()
    {
        // A move blocked by a wall is a valid no-op: the piece stays and nothing
        // is thrown. (Contrast with moving while there is no active piece.)
        var well = OpenWith(4, 10, PieceType.O);
        var before = well.Active!.Cells;

        well.MoveLeft(); // against the left wall

        Assert.IsTrue(well.Active!.Cells.SetEquals(before), "blocked move left the piece in place");
        Assert.IsFalse(well.IsGameOver);
    }

    [TestMethod]
    public void Landing_LeavesAwaiting_AndTheNextSpawnResumesPlay()
    {
        var well = OpenWith(4, 8, PieceType.O);

        well.Drop();
        Assert.IsTrue(well.IsAwaitingPiece, "between pieces after landing");
        Assert.IsNull(well.Active);

        well.Spawn(PieceType.T);
        Assert.IsFalse(well.IsAwaitingPiece, "play resumed");
        Assert.AreEqual(PieceType.T, well.Active!.Type);
    }

    [TestMethod]
    public void Collision_RejectsWallFloorAndPile_WithTheFramePredicate()
    {
        // The frame is a boundary predicate, not a cell set; collision still
        // catches all three cases through the same membership probe.
        var well = OpenWith(6, 6, PieceType.O);

        // Left wall: spawn at cols 1..2 (anchor (6-4)/2 = 1), shove left to the wall.
        well.MoveLeft(); // cols 0..1, against the wall
        var atWall = well.Active!.Cells;
        well.MoveLeft(); // blocked by the wall predicate
        Assert.IsTrue(well.Active!.Cells.SetEquals(atWall), "left wall blocks");

        // Floor + pile: drop to the floor (cols 0..1, rows 4..5), then a second
        // O — which spawns at cols 1..2 — stacks where it meets the pile.
        well.Drop(); // rests on the floor (frame predicate stopped it)
        Assert.IsTrue(well.Pile.Occupies(new Position(5, 0)), "stopped by the floor");
        well.Spawn(PieceType.O);
        well.Drop(); // second O (cols 1..2) stops on the pile under column 1
        Assert.IsTrue(well.Pile.Occupies(new Position(3, 1)), "stopped by the pile");
    }

    [TestMethod]
    public void Constructor_RejectsAWellTooSmallToAdmitAPiece()
    {
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new Well(3, 10));
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new Well(4, 1));
    }

    [TestMethod]
    public void NextPieceLetter_AlwaysReturnsAValidPieceLetter_ThatNamesAPieceType()
    {
        var well = new Well(10, 20);

        // Sample many draws; every one must be a letter that names a PieceType,
        // and the picker must not place anything (it is a pure query).
        for (var i = 0; i < 200; i++)
        {
            var letter = well.NextPieceLetter();
            Assert.IsTrue(
                System.Enum.TryParse<PieceType>(letter, out _),
                $"'{letter}' should name a PieceType");
            Assert.IsTrue(well.IsAwaitingPiece, "the picker must not place a piece");
            Assert.IsNull(well.Active);
        }
    }
}
