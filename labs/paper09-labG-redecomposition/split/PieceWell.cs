using System.Collections.Immutable;
using System.Linq;

namespace Tetris;

/// <summary>
/// The PIECE ROLE — the other of the two roles <see cref="Well"/> was re-cut into.
/// It is the well as the falling tetromino sees it: the boundary
/// <see cref="Frame"/>, the pile as last PROJECTED into it, and the one piece
/// currently falling.
/// <para>
/// It decides every collision LOCALLY. The projection is a figure like any other,
/// so wall-, floor- and pile-collision remain the single
/// <see cref="Shape.Intersects"/> test they were under the well — the re-cut did
/// not touch the legality rule, only the party that holds one of its operands.
/// Nothing is asked of the pile role while a piece is falling, and nothing needs
/// to be: the pile cannot change until this piece lands, and that landing ends
/// this piece.
/// </para>
/// <para>
/// A piece role is in one of FOUR states, where the well had three. It is
/// <em>falling</em> (<see cref="IsFalling"/>), <em>between pieces</em>
/// (<see cref="IsAwaitingPiece"/>), <em>over</em> (<see cref="IsGameOver"/>) — or
/// <em>settling</em> (<see cref="IsSettling"/>): it has landed a piece and has not
/// yet been told the pile that resulted. Settling is the state the split created.
/// Under the well, landing was one atomic transition — the pile absorbed and the
/// active slot cleared together — because one object owned both. Across two roles
/// the absorb is somebody else's act, so there is a moment in between, and a
/// caller has to be able to see it. This is not speculation: no piece exists while
/// settling, so nothing can be wrong; it is a state that has to be named.
/// </para>
/// <para>
/// Query-first contract, as before: the move verbs are valid only while a piece is
/// falling and otherwise throw <see cref="TetrisRuleException"/>; a move that is
/// merely <em>blocked</em> is a valid no-op.
/// </para>
/// </summary>
internal sealed class PieceWell
{
    /// <summary>The boundary figure — walls and floor. Held by BOTH roles.</summary>
    internal Frame Frame { get; }

    /// <summary>The tetromino currently falling, or <c>null</c> when not falling.</summary>
    internal Piece? Active { get; private set; }

    /// <summary>
    /// Whether the game is over — as TOLD by the pile role, never reckoned here.
    /// The pile role owns the fact (it is the party that knows the pile); this
    /// role holds its word for it.
    /// </summary>
    internal bool IsGameOver { get; private set; }

    /// <summary>
    /// Whether a piece has landed and the resulting pile has not yet arrived. The
    /// state the re-cut introduced; see the type remarks.
    /// </summary>
    internal bool IsSettling { get; private set; }

    /// <summary>How many pieces this role has landed.</summary>
    internal int Landings { get; private set; }

    // The pile as last projected in. Immutable for the whole lifetime of the
    // falling piece above it, which is what makes local collision decisions sound.
    private CellSet projection = CellSet.Empty;

    /// <summary>
    /// Opens a piece role over an empty well of the given interior size: no piece
    /// falling, an empty projection, awaiting its first <see cref="Spawn"/>.
    /// </summary>
    internal PieceWell(int width, int height)
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

        AssertInvariants();
    }

    /// <summary>
    /// Whether a piece is falling right now — the precondition of every move verb.
    /// Under the well this was derivable ("not over and not awaiting"); with a
    /// fourth state it is not, so the role states it.
    /// </summary>
    internal bool IsFalling => Active is not null;

    /// <summary>
    /// Whether the role is between pieces: settled, its pile known, ready for the
    /// next <see cref="Spawn"/>.
    /// </summary>
    internal bool IsAwaitingPiece => Active is null && !IsSettling && !IsGameOver;

    /// <summary>
    /// The cells the most recently landed piece came to rest on, rendered so they
    /// can be told to the pile role. Empty until the first landing.
    /// </summary>
    internal string LandedCells { get; private set; } = string.Empty;

    /// <summary>
    /// The identity of the most recent landing — what makes the utterance about it
    /// idempotent, so a redelivered "this piece landed here" is absorbed once. It
    /// counts landings rather than reading a clock, so it is reproduced exactly by
    /// a replay.
    /// </summary>
    internal string LandingToken => "land-" + Landings;

    /// <summary>The top-left corner of the 4-wide spawn bounding box.</summary>
    private Position SpawnAnchor => new(0, (Frame.Width - 4) / 2);

    /// <summary>
    /// Places a piece of the given <paramref name="type"/> at the spawn anchor.
    /// Valid only when <see cref="IsAwaitingPiece"/> — spawning while a piece
    /// falls, while settling, or once over, throws.
    /// </summary>
    internal void Spawn(PieceType type)
    {
        if (Active is not null)
        {
            throw new TetrisRuleException("Cannot spawn: a piece is already falling.");
        }

        if (IsSettling)
        {
            throw new TetrisRuleException("Cannot spawn: the last landing has not settled yet.");
        }

        if (IsGameOver)
        {
            throw new TetrisRuleException("Cannot spawn: the game is over.");
        }

        Active = Tetromino.Spawn(type, SpawnAnchor);
        AssertInvariants();
    }

    // The piece-selection policy, unchanged from the well: a uniform random pick
    // over the seven types, from a TRANSIENT source that is never part of this
    // role's state and so is never persisted or replayed. A caller resolves the
    // letter once (the resolved letter is what gets recorded), then feeds it back
    // as a deterministic Spawn. So this must stay a pure query.
    private static readonly System.Random Chooser = new();
    private static readonly string[] Letters = ["I", "O", "T", "S", "Z", "J", "L"];

    /// <summary>
    /// Picks the next piece at random and returns its letter — the piece-selection
    /// policy, as a query. It places nothing.
    /// </summary>
    internal string NextPieceLetter() => Letters[Chooser.Next(Letters.Length)];

    /// <summary>
    /// The single legality rule, and it did not change: a candidate placement is
    /// legal iff its cells overlap neither the boundary nor the pile. The pile is
    /// now the projection rather than the pile object — the same figure, held by
    /// this role instead of asked of another.
    /// </summary>
    private bool Collides(Piece candidate) =>
        candidate.Intersects(Frame) || candidate.Intersects(projection);

    /// <summary>Slides the falling piece one column left; a blocked slide is a no-op.</summary>
    internal void MoveLeft() => Shift(Offset.Left);

    /// <summary>Slides the falling piece one column right; a blocked slide is a no-op.</summary>
    internal void MoveRight() => Shift(Offset.Right);

    /// <summary>
    /// Rotates the falling piece one quarter-turn in the single rotation sense; a
    /// rotation that would collide is a no-op (no wall-kicks).
    /// </summary>
    internal void Rotate()
    {
        RequireFallingPiece();

        var candidate = Active!.Rotate();
        if (!Collides(candidate))
        {
            Active = candidate;
        }

        AssertInvariants();
    }

    /// <summary>
    /// Advances one step. If the falling piece can descend a row it does;
    /// otherwise it <em>lands</em>, leaving this role settling.
    /// </summary>
    internal void Tick()
    {
        RequireFallingPiece();

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
    /// Drops the falling piece straight down until it rests, then lands it — the
    /// hard drop.
    /// </summary>
    internal void Drop()
    {
        RequireFallingPiece();

        var resting = Active!;
        while (!Collides(resting.Translate(Offset.Down)))
        {
            resting = resting.Translate(Offset.Down);
        }

        Active = resting;
        Land();
    }

    /// <summary>
    /// Takes up the pile role's answer to a landing: the pile as it now stands,
    /// and whether the game is over. This closes the settling state — the piece
    /// role's uptake of somebody else's act.
    /// <para>
    /// Being told a pile while a piece is falling would mean the pile changed under
    /// a live piece, which cannot happen, so it is refused rather than absorbed.
    /// </para>
    /// </summary>
    internal void Take(string pileCells, bool over)
    {
        if (Active is not null)
        {
            throw new TetrisRuleException("Cannot take a pile: a piece is still falling.");
        }

        projection = CellSet.Of(CellCodec.Decode(pileCells));
        IsGameOver = over;
        IsSettling = false;

        AssertInvariants();
    }

    private void Shift(Offset offset)
    {
        RequireFallingPiece();

        var candidate = Active!.Translate(offset);
        if (!Collides(candidate))
        {
            Active = candidate;
        }

        AssertInvariants();
    }

    /// <summary>Guards the move verbs: there must be a piece falling.</summary>
    private void RequireFallingPiece()
    {
        if (Active is null)
        {
            throw new TetrisRuleException(
                "No falling piece; check IsFalling / IsSettling / IsAwaitingPiece / IsGameOver before moving.");
        }
    }

    /// <summary>
    /// Lands the falling piece: its resting cells become what this role has to say
    /// about the landing, the active slot clears, and the role starts settling. It
    /// does NOT absorb anything — absorbing is the pile role's act, and this role
    /// cannot perform it.
    /// </summary>
    private void Land()
    {
        LandedCells = CellCodec.Encode(Active!.Cells);
        Active = null;
        Landings++;
        IsSettling = true;

        AssertInvariants();
    }

    /// <summary>
    /// Every occupied cell this role knows about, clipped to the interior — the
    /// falling piece's half of a rendered frame. The pile role supplies the other.
    /// </summary>
    internal ImmutableHashSet<Position> OccupiedInterior() =>
        Active is null
            ? ImmutableHashSet<Position>.Empty
            : Active.Cells.Where(Frame.Contains).ToImmutableHashSet();

    /// <summary>
    /// The piece role's invariants, re-checked after every transition. They are the
    /// well's, minus the two that were about the pile: this role cannot assert
    /// anything about a figure it does not own, and does not try.
    /// </summary>
    private void AssertInvariants()
    {
        // Game over implies no piece is falling. (Not the converse: a null active
        // piece may mean awaiting, or settling.)
        if (IsGameOver && Active is not null)
        {
            throw new TetrisRuleException("The game is over but a piece is still falling.");
        }

        // Settling implies no piece is falling — the landing cleared the slot.
        if (IsSettling && Active is not null)
        {
            throw new TetrisRuleException("A landing is settling but a piece is still falling.");
        }

        if (Active is null)
        {
            return;
        }

        // THE invariant the split had to keep: the falling piece rests in free
        // space — it touches neither the boundary nor the pile.
        if (Collides(Active))
        {
            throw new TetrisRuleException($"The falling piece {Active} intersects an occupied figure.");
        }

        // Every active cell lies within the interior column range. (Active cells
        // may sit above row 0, in the open sky, before they fall in.)
        foreach (var cell in Active.Cells)
        {
            if (cell.Column < 0 || cell.Column >= Frame.Width || cell.Row >= Frame.Height)
            {
                throw new TetrisRuleException($"Active cell {cell} lies outside the frame.");
            }
        }
    }
}
