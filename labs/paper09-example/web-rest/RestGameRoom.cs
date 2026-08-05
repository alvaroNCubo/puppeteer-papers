using Tetris.Acting;

namespace Tetris.WebRest;

/// <summary>
/// One session's game for the REST lab: a <see cref="TetrisActor"/> (the SAME
/// clean Well over the Performance host) paired with an <see cref="SseSink"/>.
/// The two seams are physically separate here — input arrives on a POST and is
/// applied by <see cref="Apply"/>; output leaves on the SSE channel the sink
/// owns. (A deliberate sibling of the WebSocket lab's GameRoom, so the two labs
/// stay independent; the orchestration is identical.)
/// <para>
/// Actor ops run under a per-room lock: the actor is a serial reducer, so
/// concurrent POSTs to one session apply one move at a time.
/// </para>
/// </summary>
public sealed class RestGameRoom : IDisposable
{
    private const int Width = 10;
    private const int Height = 20;

    private readonly object gate = new();
    private readonly TetrisActor game;

    public SseSink Sink { get; }
    public string Session { get; }

    public RestGameRoom(string session)
    {
        Session = session;
        Sink = new SseSink();

        // Same OnPerformance path as every runner, with the SSE sink as the
        // OutputTarget. FileSystem storage keyed by session; the frame Job reaction
        // is wired by the actor. (A distinct journal dir from the WS lab.)
        game = TetrisActor.OnPerformance(session, Width, Height, SessionPaths.For("rest-" + session), Sink);

        lock (gate)
        {
            if (game.Snapshot().IsAwaitingPiece) game.SpawnNext();
            game.RunReactions(); // opening frame -> SSE
        }
    }

    /// <summary>
    /// Applies a logical move (left|right|rotate|tick|drop) — the InputSource half.
    /// Check-guarded verbs; tick/drop then spawn-if-awaiting; then RunReactions
    /// pushes the frame out the SSE channel (NOT back in the POST response — the
    /// separation is the point). Unknown moves are ignored. Serialised per room.
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
                default: return;
            }

            game.RunReactions();
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

#pragma warning disable VSTHRD002
        Sink.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
