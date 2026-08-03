using Tetris.Hex;
using Tetris.Hex.Adapters;

namespace Tetris.HexWebRest;

/// <summary>
/// One session's game for the REST staging: the same hexagon behind its driving
/// port, wired to an <see cref="SseBoardOutput"/> and the shared piece-selection
/// adapter. A deliberate sibling of staging 2's <c>GameRoom</c>, so the two
/// stagings stay independent; the composition is identical and only the output
/// adapter differs.
/// <para>
/// Driving-port calls run under a per-room lock: the hexagon is a serial reducer,
/// so concurrent POSTs to one session apply one move at a time.
/// </para>
/// </summary>
public sealed class RestGameRoom : IDisposable
{
    private const int Width = 10;
    private const int Height = 20;

    private readonly object gate = new();
    private readonly IGameCommandPort game;

    public SseBoardOutput Output { get; }
    public string Session { get; }

    public RestGameRoom(string session)
    {
        Session = session;
        Output = new SseBoardOutput();
        game = new GameService(Width, Height, Output, new RandomPieceSelection());

        lock (gate)
        {
            game.Start(); // opening frame -> SSE
        }
    }

    /// <summary>
    /// Applies a logical move (left|right|rotate|tick|drop) — the input half. The
    /// frame does NOT come back in the POST response; it goes out the SSE channel.
    /// Unknown moves are ignored. Serialised per room.
    /// </summary>
    public void Apply(string move)
    {
        lock (gate)
        {
            switch (move)
            {
                case "left": game.MoveLeft(); break;
                case "right": game.MoveRight(); break;
                case "rotate": game.Rotate(); break;
                case "tick": game.Tick(); break;
                case "drop": game.Drop(); break;
                default: return;
            }
        }
    }

    public void Dispose()
    {
#pragma warning disable VSTHRD002
        Output.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
