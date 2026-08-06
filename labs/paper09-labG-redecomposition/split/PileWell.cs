using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// The PILE ROLE — one of the two roles <see cref="Well"/> was re-cut into. It is
/// the well as the settled blocks see it: the boundary <see cref="Frame"/> and the
/// accumulated <see cref="Pile"/>, and nothing about a falling piece.
/// <para>
/// It has exactly one verb, <see cref="Absorb"/>: it is told the cells a piece
/// came to rest on, it absorbs them, and the pile collapses whatever rows they
/// completed. Everything else it offers is a query — the new pile as a
/// <see cref="Projection"/> for the piece role to collide against, the running
/// <see cref="ClearedLines"/>, and <see cref="IsGameOver"/>.
/// </para>
/// <para>
/// It owns game over. The fact is derived exactly as the well derived it — the
/// pile has risen into the spawn region — and it is derived HERE and nowhere else.
/// The piece role could compute it too, from the projection it holds, and for that
/// very reason must not: two roles reckoning the same fact can disagree, and then
/// there is no fact of the matter. So the piece role is TOLD.
/// </para>
/// <para>
/// Why this split is sound, and it is the whole reason it works: the pile is
/// immutable for the entire lifetime of a falling piece. It changes only when a
/// piece lands, and that landing is the act that ends the piece's life. The two
/// phases never overlap, so the projection the piece role holds is never stale
/// while it is being used, and nothing anywhere is speculated.
/// </para>
/// </summary>
internal sealed class PileWell
{
    /// <summary>The boundary figure — walls and floor. Held by BOTH roles.</summary>
    internal Frame Frame { get; }

    /// <summary>The accumulated landed blocks.</summary>
    internal Pile Pile { get; private set; }

    /// <summary>How many rows have collapsed over this role's lifetime.</summary>
    internal int ClearedLines { get; private set; }

    /// <summary>How many landings this role has absorbed.</summary>
    internal int Absorptions { get; private set; }

    /// <summary>How many rows the most recent <see cref="Absorb"/> collapsed.</summary>
    internal int LastCollapsed { get; private set; }

    /// <summary>
    /// Opens an empty pile for a well of the given interior size. The dimensions
    /// are the piece role's too — the frame is the one thing the re-cut had to
    /// give to both halves, because one needs it to decide collision and the other
    /// to decide what "a complete row" means.
    /// </summary>
    internal PileWell(int width, int height)
    {
        if (width < 4)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(width), width, "A well must be at least 4 columns wide to admit a piece.");
        }

        if (height < 2)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(height), height, "A well must be at least 2 rows tall to admit a piece.");
        }

        Frame = new Frame(width, height);
        Pile = Pile.Empty(width);

        AssertInvariants();
    }

    /// <summary>
    /// Whether the game has ended — <em>derived</em>, not stored, and derived
    /// here alone. The game is over exactly when the pile has risen into the
    /// spawn region, so a freshly spawned piece would have nowhere clear to
    /// appear.
    /// </summary>
    internal bool IsGameOver => SpawnRegion.Intersects(Pile);

    /// <summary>
    /// The pile as the piece role needs to see it: every landed cell, rendered
    /// canonically so it can travel as one value. This is the ONLY thing the piece
    /// role learns about the pile, and it is enough — collision is membership.
    /// </summary>
    internal string Projection => CellCodec.Encode(Pile.Cells);

    /// <summary>The top-left corner of the 4-wide spawn bounding box.</summary>
    private Position SpawnAnchor => new(0, (Frame.Width - 4) / 2);

    /// <summary>
    /// The cells a freshly spawned piece is born into: the top two rows across
    /// the four spawn columns. If the pile reaches any of them the next piece
    /// cannot appear — that is game over.
    /// </summary>
    private Shape SpawnRegion
    {
        get
        {
            var anchorColumn = SpawnAnchor.Column;
            var cells =
                from row in Enumerable.Range(0, 2)
                from column in Enumerable.Range(anchorColumn, 4)
                select new Position(row, column);
            return CellSet.Of(cells);
        }
    }

    /// <summary>
    /// Absorbs the cells a piece came to rest on — the pile role's one act. The
    /// <see cref="Pile"/> owns the whole transition, exactly as it did under the
    /// well: it merges the cells and collapses every row they completed. This role
    /// only records what collapsed.
    /// <para>
    /// The cells arrive as a rendering because they crossed a role boundary. They
    /// are checked before they are believed: a tetromino is four cells, they must
    /// lie inside the frame, and they must not already be occupied. Those checks
    /// are the pile role's own — under the well they were the well's invariant,
    /// asserted after the fact; here they are a precondition on being told
    /// something, which is the stronger position.
    /// </para>
    /// </summary>
    internal void Absorb(string landedCells)
    {
        var cells = CellCodec.Decode(landedCells);

        if (cells.Count != Piece.CellCount)
        {
            throw new TetrisRuleException(
                $"Cannot absorb {cells.Count} cells; a landed tetromino has exactly {Piece.CellCount}.");
        }

        foreach (var cell in cells)
        {
            if (!Frame.Contains(cell))
            {
                throw new TetrisRuleException($"Cannot absorb {cell}: it lies outside the frame.");
            }

            if (Pile.Occupies(cell))
            {
                throw new TetrisRuleException($"Cannot absorb {cell}: the pile already occupies it.");
            }
        }

        (Pile, var collapsed) = Pile.Integrate(new CellSet(cells));
        LastCollapsed = collapsed.Count;
        ClearedLines += collapsed.Count;
        Absorptions++;

        AssertInvariants();
    }

    /// <summary>
    /// Every occupied cell this role knows about, clipped to the interior — the
    /// pile's half of a rendered frame. The piece role supplies the other half.
    /// </summary>
    internal ImmutableHashSet<Position> OccupiedInterior() =>
        Pile.Cells.Where(Frame.Contains).ToImmutableHashSet();

    /// <summary>
    /// The pile role's invariants, re-checked after every transition — the same
    /// two the well asserted about the pile, now asserted by the role that owns
    /// it. Nothing about a falling piece appears here, because nothing about a
    /// falling piece is this role's business.
    /// </summary>
    private void AssertInvariants()
    {
        // The pile never retains a complete row.
        if (Pile.HasCompleteRow())
        {
            throw new TetrisRuleException("The pile retained a complete row.");
        }

        // Every landed cell lies inside the frame.
        foreach (var cell in Pile.Cells)
        {
            if (!Frame.Contains(cell))
            {
                throw new TetrisRuleException($"Pile cell {cell} lies outside the frame.");
            }
        }
    }
}
