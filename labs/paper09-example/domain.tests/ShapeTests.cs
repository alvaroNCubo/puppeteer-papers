using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Tests;

/// <summary>
/// The spine of the model: that a piece, the pile and the frame are all the
/// same kind of figure, and that one <see cref="Shape.Intersects"/> test
/// answers collision for all of them.
/// </summary>
[TestClass]
public sealed class ShapeTests
{
    [TestMethod]
    public void Frame_IsABoundaryPredicate_OccupiesWallsAndFloor_NotTheInterior()
    {
        var frame = new Frame(width: 4, height: 3);

        // Walls at columns < 0 and >= width; floor at row >= height.
        Assert.IsTrue(frame.Occupies(new Position(0, -1)), "left wall");
        Assert.IsTrue(frame.Occupies(new Position(0, 4)), "right wall");
        Assert.IsTrue(frame.Occupies(new Position(3, 0)), "floor");

        // Interior cells are not part of the frame.
        Assert.IsFalse(frame.Occupies(new Position(0, 0)), "interior top-left");
        Assert.IsFalse(frame.Occupies(new Position(2, 3)), "interior bottom-right");

        // The top is open: a row above 0 is not boundary.
        Assert.IsFalse(frame.Occupies(new Position(-1, 1)), "the open sky above is not boundary");

        // The boundary is a predicate, not a materialised set.
        Assert.ThrowsException<System.NotSupportedException>(() => _ = frame.Cells);
    }

    [TestMethod]
    public void Frame_Contains_OnlyInteriorColumnsAboveFloor()
    {
        var frame = new Frame(width: 4, height: 3);

        Assert.IsTrue(frame.Contains(new Position(0, 0)));
        Assert.IsTrue(frame.Contains(new Position(2, 3)));
        Assert.IsTrue(frame.Contains(new Position(-2, 1)), "the open sky above is interior");

        Assert.IsFalse(frame.Contains(new Position(0, -1)), "left wall is not interior");
        Assert.IsFalse(frame.Contains(new Position(0, 4)), "right wall is not interior");
        Assert.IsFalse(frame.Contains(new Position(3, 0)), "floor is not interior");
    }

    [TestMethod]
    public void Intersects_IsSymmetricAndDetectsSharedCells()
    {
        var a = Tetromino.Spawn(PieceType.O, new Position(0, 0)); // cells (0,0)(0,1)(1,0)(1,1)
        var overlapping = Tetromino.Spawn(PieceType.O, new Position(1, 1));
        var disjoint = Tetromino.Spawn(PieceType.O, new Position(5, 5));

        Assert.IsTrue(a.Intersects(overlapping));
        Assert.IsTrue(overlapping.Intersects(a), "symmetric");
        Assert.IsFalse(a.Intersects(disjoint));
    }

    [TestMethod]
    public void OnePiece_CollidesWithWall_PileAndFloor_ByTheSameTest()
    {
        var frame = new Frame(width: 4, height: 6);

        // Same Intersects call against the frame catches the left wall…
        var atLeftWall = Tetromino.Spawn(PieceType.O, new Position(2, -1));
        Assert.IsTrue(atLeftWall.Intersects(frame));

        // …the floor…
        var onFloor = Tetromino.Spawn(PieceType.O, new Position(5, 0)); // cells reach row 6 = floor
        Assert.IsTrue(onFloor.Intersects(frame));

        // …and against the pile, the very same test catches a landed block.
        var (pile, _) = Pile.Empty(4).Integrate(Tetromino.Spawn(PieceType.O, new Position(4, 0)));
        var ontoPile = Tetromino.Spawn(PieceType.O, new Position(3, 0)); // sits directly above
        Assert.IsTrue(ontoPile.Intersects(pile));
    }
}
