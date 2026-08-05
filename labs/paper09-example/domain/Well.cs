using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// The board — the aggregate root and the one mutable thing in the model. A
/// well composes three figures: the boundary <see cref="Frame"/>, the
/// accumulated <see cref="Pile"/>, and the falling active <see cref="Piece"/>.
/// <para>
/// The well does not decide which piece comes next — it is <em>told</em>.
/// Spawning is an inbound operation (<see cref="Spawn"/>): the caller chooses
/// the piece type (at random, from a script, or however it likes) and hands it
/// in. This keeps the well a pure, deterministic function of its construction
/// and the sequence of operations applied to it; every value object it holds is
/// immutable, and a transition replaces a reference rather than mutating.
/// </para>
/// <para>
/// A well is in one of three states: a piece is <em>falling</em>
/// (<see cref="Active"/> is non-null), <em>between pieces</em>
/// (<see cref="IsAwaitingPiece"/> — settled, waiting for the next
/// <see cref="Spawn"/>), or <em>over</em> (<see cref="IsGameOver"/>). So a null
/// active piece does not by itself mean game over.
/// </para>
/// <para>
/// Query-first contract. The operations are valid only in the right state and
/// otherwise throw <see cref="TetrisRuleException"/>: <see cref="Spawn"/> only
/// when awaiting a piece; the move verbs only when a piece is active. A caller
/// checks the queries first. A move that is merely <em>blocked</em> (it would
/// collide with a wall or the pile) is different: it is a valid no-op — the
/// piece stays put and nothing is thrown.
/// </para>
/// </summary>
internal sealed class Well
{
    /// <summary>The boundary figure — walls and floor.</summary>
    internal Frame Frame { get; }

    /// <summary>The accumulated landed blocks.</summary>
    internal Pile Pile { get; private set; }

    /// <summary>The tetromino currently falling, or <c>null</c> between pieces / once over.</summary>
    internal Piece? Active { get; private set; }

    /// <summary>How many rows have been cleared over the well's lifetime.</summary>
    internal int ClearedLines { get; private set; }

    /// <summary>
    /// Opens an empty well of the given interior size — no active piece, no pile,
    /// awaiting its first <see cref="Spawn"/>. Choosing and supplying pieces is
    /// the caller's job.
    /// </summary>
    internal Well(int width, int height)
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
    /// Whether the game has ended — <em>derived</em>, not stored. The game is
    /// over exactly when the pile has risen into the <see cref="SpawnRegion"/>,
    /// so a freshly spawned piece would have nowhere clear to appear.
    /// </summary>
    internal bool IsGameOver => SpawnRegion.Intersects(Pile);

    /// <summary>
    /// Whether the well is between pieces: settled and ready for the next
    /// <see cref="Spawn"/>. True exactly when there is no active piece and the
    /// game is not over.
    /// </summary>
    internal bool IsAwaitingPiece => Active is null && !IsGameOver;

    /// <summary>The top-left corner of the 4-wide spawn bounding box.</summary>
    private Position SpawnAnchor => new(0, (Frame.Width - 4) / 2);

    /// <summary>
    /// The cells a freshly spawned piece is born into: the top two rows across
    /// the four spawn columns. Every tetromino's spawn pose lies within this
    /// box, so if the pile reaches any of these cells the next piece cannot
    /// appear — that is game over.
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
            return new CellSet(cells.ToImmutableHashSet());
        }
    }

    /// <summary>
    /// Places a piece of the given <paramref name="type"/> at the spawn anchor.
    /// Valid only when <see cref="IsAwaitingPiece"/>; spawning while a piece is
    /// already falling, or when the game is over, throws. (When the well is
    /// awaiting a piece the spawn region is clear by the definition of
    /// <see cref="IsGameOver"/>, so the placed piece always fits.)
    /// </summary>
    internal void Spawn(PieceType type)
    {
        if (Active is not null)
        {
            throw new TetrisRuleException("Cannot spawn: a piece is already falling.");
        }

        if (IsGameOver)
        {
            throw new TetrisRuleException("Cannot spawn: the game is over.");
        }

        Active = Tetromino.Spawn(type, SpawnAnchor);
        AssertInvariants();
    }

    // The piece-selection policy: a uniform random pick over the seven types.
    // The randomness is TRANSIENT — a process-wide source that is never part of
    // the well's state and so is never persisted or replayed. A caller resolves
    // the next letter once (the resolved letter is what gets recorded), then
    // feeds it back in as a deterministic Spawn; replay re-applies that exact
    // Spawn and never re-rolls. So this method must stay a pure query: it picks
    // a letter and mutates nothing.
    private static readonly System.Random Chooser = new();
    private static readonly string[] Letters = ["I", "O", "T", "S", "Z", "J", "L"];

    /// <summary>
    /// Picks the next piece at random and returns its letter (one of
    /// "I", "O", "T", "S", "Z", "J", "L") — the piece-selection policy, as a
    /// query. It does not place anything; a caller resolves the letter and then
    /// spawns it. The letter names a <see cref="PieceType"/> member, so a host
    /// can coerce it back to the enum.
    /// </summary>
    internal string NextPieceLetter() => Letters[Chooser.Next(Letters.Length)];

    /// <summary>
    /// The single legality rule. A candidate placement is legal iff its cells
    /// overlap neither the boundary nor the pile. Because the frame is itself a
    /// figure, wall-, floor- and pile-collision are this one
    /// <see cref="Shape.Intersects"/> test, not three special cases. The
    /// four-cell piece is the small figure that iterates; the frame and pile are
    /// only probed.
    /// </summary>
    private bool Collides(Piece candidate) =>
        candidate.Intersects(Frame) || candidate.Intersects(Pile);

    /// <summary>Slides the active piece one column left; a blocked slide is a no-op.</summary>
    internal void MoveLeft() => Shift(Offset.Left);

    /// <summary>Slides the active piece one column right; a blocked slide is a no-op.</summary>
    internal void MoveRight() => Shift(Offset.Right);

    /// <summary>
    /// Rotates the active piece one quarter-turn in the single rotation sense. A
    /// rotation that would collide with a wall or the pile is rejected as a
    /// no-op (no wall-kicks; see the README). Throws if no piece is active.
    /// </summary>
    internal void Rotate()
    {
        RequireActivePiece();

        var candidate = Active!.Rotate();
        if (!Collides(candidate))
        {
            Active = candidate;
        }

        AssertInvariants();
    }

    /// <summary>
    /// Advances the world by one step. If the active piece can descend a row it
    /// does; otherwise it <em>lands</em>, leaving the well between pieces. Throws
    /// if no piece is active.
    /// </summary>
    internal void Tick()
    {
        RequireActivePiece();

        var descended = Active!.Translate(Offset.Down);
        if (!Collides(descended))
        {
            Active = descended;
            AssertInvariants();
            return;
        }

        Land();
    }

    /// <summary>
    /// Drops the active piece straight down until it rests, then lands it — the
    /// hard drop. Throws if no piece is active.
    /// </summary>
    internal void Drop()
    {
        RequireActivePiece();

        var resting = Active!;
        while (!Collides(resting.Translate(Offset.Down)))
        {
            resting = resting.Translate(Offset.Down);
        }

        Active = resting;
        Land();
    }

    private void Shift(Offset offset)
    {
        RequireActivePiece();

        var candidate = Active!.Translate(offset);
        if (!Collides(candidate))
        {
            Active = candidate;
        }

        AssertInvariants();
    }

    /// <summary>Guards the move verbs: there must be a piece to move.</summary>
    private void RequireActivePiece()
    {
        if (Active is null)
        {
            throw new TetrisRuleException(
                "No active piece; check Active / IsAwaitingPiece / IsGameOver before moving.");
        }
    }

    /// <summary>
    /// Settles the active piece into the pile and leaves the well between pieces.
    /// The pile owns the whole transition — it absorbs the piece and collapses
    /// any completed rows — so landing here is just: hand the piece over, count
    /// what collapsed, and clear the active slot. It does <em>not</em> spawn the
    /// next piece; the caller does that with <see cref="Spawn"/> once it sees
    /// <see cref="IsAwaitingPiece"/>.
    /// </summary>
    private void Land()
    {
        (Pile, var collapsed) = Pile.Integrate(Active!);
        ClearedLines += collapsed.Count;
        Active = null;

        AssertInvariants();
    }

    /// <summary>
    /// Re-checks the well's invariants after every transition. These never fire
    /// in correct play; they are the executable statement of what "a valid
    /// well" means, and the safety net that turns any modelling bug into a loud
    /// failure rather than silent corruption.
    /// </summary>
    private void AssertInvariants()
    {
        // Game over implies there is no active piece. (The converse need not
        // hold: a null active piece may simply mean the well is between pieces.)
        if (IsGameOver && Active is not null)
        {
            throw new TetrisRuleException("The game is over but a piece is still active.");
        }

        // The pile never retains a complete row.
        if (Pile.HasCompleteRow())
        {
            throw new TetrisRuleException("The pile retained a complete row.");
        }

        // Every landed cell lies inside the frame (interior columns, above floor).
        foreach (var cell in Pile.Cells)
        {
            if (!Frame.Contains(cell))
            {
                throw new TetrisRuleException($"Pile cell {cell} lies outside the frame.");
            }
        }

        if (Active is null)
        {
            return;
        }

        // The active piece rests in free space — it touches neither the
        // boundary nor the pile.
        if (Collides(Active))
        {
            throw new TetrisRuleException($"The active piece {Active} intersects an occupied figure.");
        }

        // Every active cell lies within the interior column range. (Active
        // cells may sit above row 0, in the open sky, before they fall in.)
        foreach (var cell in Active.Cells)
        {
            if (cell.Column < 0 || cell.Column >= Frame.Width || cell.Row >= Frame.Height)
            {
                throw new TetrisRuleException($"Active cell {cell} lies outside the frame.");
            }
        }
    }

    /// <summary>
    /// A read-only snapshot of every occupied interior cell — the union of the
    /// pile and the active piece, clipped to the interior. Handy for rendering
    /// and for asserting state without exposing mutable internals.
    /// </summary>
    internal ImmutableHashSet<Position> OccupiedInterior()
    {
        var occupied = Pile.Cells;
        if (Active is not null)
        {
            occupied = occupied.Union(Active.Cells);
        }

        return occupied.Where(Frame.Contains).ToImmutableHashSet();
    }

    /// <summary>
    /// A bare set of cells as a <see cref="Shape"/> — used for the spawn region,
    /// which is just a handful of cells with no behaviour of its own.
    /// </summary>
    private sealed class CellSet : Shape
    {
        public CellSet(ImmutableHashSet<Position> cells) => Cells = cells;

        public override ImmutableHashSet<Position> Cells { get; }
    }
}
