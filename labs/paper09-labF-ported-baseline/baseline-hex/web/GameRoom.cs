using Tetris.Hex;
using Tetris.Hex.Adapters;

namespace Tetris.HexWeb;

/// <summary>
/// One session's game: a hexagon (<see cref="GameService"/> behind its driving
/// port) wired to a <see cref="WebSocketBoardOutput"/> and the shared
/// piece-selection adapter. The web staging is exactly this composition plus a
/// transport — the rules are untouched.
/// <para>
/// All driving-port calls run under a per-room lock: the hexagon is a serial
/// reducer (the well mutates), so several players sharing one session submit
/// concurrently but the room applies one move at a time.
/// </para>
/// </summary>
public sealed class GameRoom : IDisposable
{
    private const int Width = 10;
    private const int Height = 20;

    private readonly object gate = new();
    private readonly IGameCommandPort game;

    public WebSocketBoardOutput Output { get; }
    public string Session { get; }

    public GameRoom(string session)
    {
        Session = session;
        Output = new WebSocketBoardOutput();
        game = new GameService(Width, Height, Output, new RandomPieceSelection());

        lock (gate)
        {
            game.Start(); // first piece + opening frame, presented through the port
        }
    }

    /// <summary>
    /// Applies a logical move (left|right|rotate|tick|drop). The verbs are gentle,
    /// so the transport does not have to know the game's state; unknown moves are
    /// ignored. Serialised per room.
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
                default: return; // unknown move: ignore
            }
        }
    }

    public void Dispose()
    {
        // Dispose is synchronous; the output pump completes promptly. Intentional.
#pragma warning disable VSTHRD002
        Output.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
