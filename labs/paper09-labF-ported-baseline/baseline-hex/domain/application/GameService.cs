namespace Tetris.Hex;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// The application service at the centre of the hexagon: it implements the
/// driving port <see cref="IGameCommandPort"/> and depends on the driven ports
/// <see cref="IBoardOutputPort"/> and <see cref="IPieceSelectionPort"/>. It
/// owns the same orchestration the journaled example's hosts perform — the
/// query-first guard before each verb, and feeding the next piece once one
/// lands — and it owns nothing else: the rules are all in <see cref="Well"/>,
/// which is the byte-for-byte model of the journaled domain.
/// <para>
/// This is the type that makes the arrangement hexagonal rather than merely
/// layered: it cannot be constructed without both driven ports, so the
/// contract for where output goes and where the next piece comes from is on
/// the hexagon's own boundary. That is the property being measured against the
/// journaled arrangement, in which the same two decisions are bound entirely
/// outside the domain.
/// </para>
/// </summary>
public sealed class GameService : IGameCommandPort
{
    private readonly Well well;
    private readonly IBoardOutputPort output;
    private readonly IPieceSelectionPort pieces;

    // Staging 4's addition. Null for the three stagings that keep the hexagon in
    // memory for the life of the process (console, WebSocket, REST), non-null for
    // the client that runs one operation per process and must reload each time.
    private readonly IGameStatePort? state;
    private readonly string? session;

    /// <summary>
    /// Opens a well of the given interior size, wired to its driven ports.
    /// Nothing is presented until <see cref="Start"/> is called. State is kept in
    /// memory for the life of this instance.
    /// </summary>
    public GameService(int width, int height, IBoardOutputPort output, IPieceSelectionPort pieces)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        well = new Well(width, height);
    }

    /// <summary>
    /// Opens or REOPENS the well of <paramref name="session"/> through a state
    /// port: if the port holds a state for that session the well resumes from it,
    /// otherwise a fresh one is opened. Every operation is recorded back through
    /// the port before it is presented.
    /// <para>
    /// Added by staging 4. Its existence is the honest cost of a client that keeps
    /// nothing in memory between operations: three ports instead of two, a second
    /// constructor, and a conditional on every write.
    /// </para>
    /// </summary>
    public GameService(
        string session,
        int width,
        int height,
        IBoardOutputPort output,
        IPieceSelectionPort pieces,
        IGameStatePort state)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        var saved = state.Load(session);
        well = saved is null ? new Well(width, height) : Reopen(saved);
    }

    /// <inheritdoc />
    public void Start()
    {
        SpawnIfAwaiting();
        Commit();
    }

    /// <inheritdoc />
    public void Show() => Present();

    /// <inheritdoc />
    public void MoveLeft() => Move(well.MoveLeft);

    /// <inheritdoc />
    public void MoveRight() => Move(well.MoveRight);

    /// <inheritdoc />
    public void Rotate() => Move(well.Rotate);

    /// <inheritdoc />
    public void Tick() => Advance(well.Tick);

    /// <inheritdoc />
    public void Drop() => Advance(well.Drop);

    // A plain move: guarded, applied, presented. The guard is what keeps the
    // verb gentle — the domain's hard rule exception is the backstop, never the
    // control flow.
    private void Move(Action verb)
    {
        if (!IsPieceFalling)
        {
            return;
        }

        verb();
        Commit();
    }

    // Gravity and the hard drop can LAND the piece, which leaves the well
    // between pieces; the application feeds the next one before presenting, so
    // a client never sees an empty board mid-play.
    private void Advance(Action verb)
    {
        if (!IsPieceFalling)
        {
            return;
        }

        verb();
        SpawnIfAwaiting();
        Commit();
    }

    private bool IsPieceFalling => !well.IsGameOver && !well.IsAwaitingPiece;

    private void SpawnIfAwaiting()
    {
        if (!well.IsAwaitingPiece)
        {
            return;
        }

        var letter = pieces.NextPieceLetter();
        if (!Enum.TryParse<PieceType>(letter, out var type))
        {
            throw new TetrisRuleException(
                $"The piece-selection port returned '{letter}', which does not name a tetromino.");
        }

        well.Spawn(type);
    }

    // Record then present. Recording comes first so a client that dies between the
    // two loses a frame (recoverable — the next Show re-presents) rather than a move.
    private void Commit()
    {
        Persist();
        Present();
    }

    private void Persist()
    {
        if (state is null)
        {
            return;
        }

        state.Save(session!, new GameState(
            well.Frame.Width,
            well.Frame.Height,
            well.ClearedLines,
            well.Pile.Cells.Select(cell => new BoardCell(cell.Row, cell.Column)).ToList(),
            well.Active?.Type.ToString(),
            well.Active?.Anchor.Row ?? 0,
            well.Active?.Anchor.Column ?? 0,
            well.Active?.Orientation.Index ?? 0));
    }

    // Rebuilds the well a state port handed back. The active piece is reconstructed
    // by spawning its type at its anchor and turning it to its recorded pose, which
    // the model already supports; the pile needed the new Pile.Of seam.
    private static Well Reopen(GameState saved)
    {
        Piece? active = null;
        if (saved.ActiveType is not null)
        {
            if (!Enum.TryParse<PieceType>(saved.ActiveType, out var type))
            {
                throw new TetrisRuleException(
                    $"Saved state names an active piece '{saved.ActiveType}', which is not a tetromino.");
            }

            active = Tetromino.Spawn(type, new Position(saved.ActiveAnchorRow, saved.ActiveAnchorColumn));
            for (var turn = 0; turn < saved.ActiveOrientation; turn++)
            {
                active = active.Rotate();
            }
        }

        return new Well(
            saved.Width,
            saved.Height,
            saved.PileCells.Select(cell => new Position(cell.Row, cell.Column)).ToImmutableHashSet(),
            saved.ClearedLines,
            active);
    }

    // The one place the model becomes a view. Everything a client could want is
    // in the port's contract, so no adapter reaches back into the well.
    private void Present() => output.Present(View());

    private BoardView View() => new(
        well.Frame.Width,
        well.Frame.Height,
        well.OccupiedInterior()
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .Select(cell => new BoardCell(cell.Row, cell.Column))
            .ToList(),
        well.ClearedLines,
        well.IsGameOver,
        well.IsAwaitingPiece,
        well.Active?.Type.ToString());
}
