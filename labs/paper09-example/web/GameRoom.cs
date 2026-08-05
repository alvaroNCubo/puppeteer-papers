using Tetris.Acting;

namespace Tetris.Web;

/// <summary>
/// One session's game: a <see cref="TetrisActor"/> (the SAME clean Well over the
/// Performance host) paired with a <see cref="WebSocketSink"/> that pushes each
/// emitted frame to the session's browser sockets. The web host is just this
/// pairing — a WebSocket InputSource feeding the actor's Check-guarded verbs, and
/// the OutputTarget sink fanning the frame out. Zero domain edits.
/// <para>
/// All actor operations run under a per-room lock: the TetrisActor is a serial
/// reducer (not thread-safe across concurrent commands), so several players
/// sharing one session (W2 scenario 1) submit concurrently but the room applies
/// one move at a time — the deterministic serial flow, same as the InputSource
/// stage's merge channel.
/// </para>
/// </summary>
public sealed class GameRoom : IDisposable
{
    private const int Width = 10;
    private const int Height = 20;

    private readonly object gate = new();
    private readonly TetrisActor game;

    public WebSocketSink Sink { get; }
    public string Session { get; }

    public GameRoom(string session)
    {
        Session = session;
        Sink = new WebSocketSink();

        // Same OnPerformance path the other runners use, with the WebSocket sink as
        // the OutputTarget. FileSystem storage keyed by session (rehydrates/replays
        // like v3); the frame Job reaction is wired by the actor.
        game = TetrisActor.OnPerformance(session, Width, Height, SessionPaths.For("web-" + session), Sink);

        // Start the game: first piece + opening frame.
        lock (gate)
        {
            if (game.Snapshot().IsAwaitingPiece) game.SpawnNext();
            game.RunReactions();
        }
    }

    /// <summary>
    /// Applies a logical move (left|right|rotate|tick|drop) and pushes the frame.
    /// Same orchestration as every other runner: Check-guarded verbs; tick/drop
    /// then spawn-if-awaiting. Unknown moves are ignored. Serialised per room.
    /// </summary>
    public void Apply(string move)
    {
        lock (gate)
        {
            var s = game.Snapshot();
            var active = !s.IsAwaitingPiece && !s.IsGameOver;

            switch (move)
            {
                case "left": if (active) game.MoveLeft(); break;
                case "right": if (active) game.MoveRight(); break;
                case "rotate": if (active) game.Rotate(); break;
                case "tick": if (active) { game.Tick(); SpawnIfAwaiting(); } break;
                case "drop": if (active) { game.Drop(); SpawnIfAwaiting(); } break;
                default: return; // unknown move: ignore
            }

            game.RunReactions(); // the reaction pushes the frame to the sink
        }
    }

    private void SpawnIfAwaiting()
    {
        if (game.Snapshot().IsAwaitingPiece) game.SpawnNext();
    }

    public void Dispose()
    {
        lock (gate)
        {
            game.Dispose();
        }

        // Dispose is synchronous; the sink's pump completes promptly. Intentional.
#pragma warning disable VSTHRD002
        Sink.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
