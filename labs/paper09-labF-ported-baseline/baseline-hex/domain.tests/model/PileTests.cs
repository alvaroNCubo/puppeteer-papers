using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Hex.Tests;

/// <summary>
/// The accumulated floor. The pile owns the whole landing transition:
/// <see cref="Pile.Integrate"/> absorbs a piece and, in the same call, collapses
/// any rows it completed (bottom-up), reporting which rows collapsed. These
/// tests drive that one operation with explicitly placed pieces.
/// </summary>
[TestClass]
public sealed class PileTests
{
    /// <summary>A tetromino of <paramref name="type"/> with its bounding box anchored at (row, col).</summary>
    private static Piece Place(PieceType type, int row, int column) =>
        Tetromino.Spawn(type, new Position(row, column));

    [TestMethod]
    public void Integrate_WithoutCompletingARow_AddsTheCellsAndReportsNoCollapse()
    {
        // Width 4. A single O at rows 2..3, cols 0..1 completes nothing.
        var (pile, collapsed) = Pile.Empty(4).Integrate(Place(PieceType.O, 2, 0));

        Assert.AreEqual(0, collapsed.Count, "no rows collapsed");
        foreach (var cell in Place(PieceType.O, 2, 0).Cells)
        {
            Assert.IsTrue(pile.Occupies(cell), $"pile should contain {cell}");
        }
        Assert.IsFalse(pile.HasCompleteRow());
    }

    [TestMethod]
    public void Integrate_CompletingTheFloorRow_CollapsesItAndReportsTheIndex()
    {
        // Width 4. A horizontal I at row 1 (cells row 1, cols 0..3) fills the
        // whole width of that row, so integrating it completes and clears row 1.
        var iBar = Place(PieceType.I, 0, 0); // I pose 0: cells (1,0)(1,1)(1,2)(1,3)
        var (pile, collapsed) = Pile.Empty(4).Integrate(iBar);

        CollectionAssert.AreEqual(new[] { 1 }, collapsed.ToArray(), "row 1 collapsed");
        Assert.AreEqual(0, pile.Cells.Count, "the completed row left an empty pile");
        Assert.IsFalse(pile.HasCompleteRow());
    }

    [TestMethod]
    public void Integrate_CompletingTwoRowsAtOnce_ReportsBoth_AndDropsTheSurvivorBottomUp()
    {
        // Width 4. Build a survivor above two rows that will complete together.
        //   O #1 -> rows 4..5, cols 0..1     (no completion)
        //   O #2 stacked -> rows 2..3, cols 0..1  (the survivor block)
        //   O #3 -> rows 4..5, cols 2..3     completes rows 4 and 5
        var pile = Pile.Empty(4);
        (pile, _) = pile.Integrate(Place(PieceType.O, 4, 0)); // rows 4..5, cols 0..1
        (pile, _) = pile.Integrate(Place(PieceType.O, 2, 0)); // rows 2..3, cols 0..1
        var (settled, collapsed) = pile.Integrate(Place(PieceType.O, 4, 2)); // rows 4..5, cols 2..3

        CollectionAssert.AreEqual(new[] { 4, 5 }, collapsed.ToArray(), "rows 4 and 5 collapsed");
        Assert.IsFalse(settled.HasCompleteRow());
        Assert.AreEqual(4, settled.Cells.Count, "only the 2x2 survivor remains");

        // The survivor (was rows 2..3, cols 0..1) had two cleared rows below it,
        // so it drops by two -> rows 4..5.
        Assert.IsTrue(settled.Occupies(new Position(4, 0)));
        Assert.IsTrue(settled.Occupies(new Position(4, 1)));
        Assert.IsTrue(settled.Occupies(new Position(5, 0)));
        Assert.IsTrue(settled.Occupies(new Position(5, 1)));
    }

    [TestMethod]
    public void Integrate_NeverLeavesACompleteRow()
    {
        // Whatever is integrated, the returned pile holds no complete row.
        var pile = Pile.Empty(4);
        (pile, _) = pile.Integrate(Place(PieceType.I, 0, 0)); // completes + clears row 1
        (pile, _) = pile.Integrate(Place(PieceType.O, 4, 0));
        (pile, _) = pile.Integrate(Place(PieceType.O, 4, 2)); // completes + clears rows 4,5

        Assert.IsFalse(pile.HasCompleteRow());
    }
}
