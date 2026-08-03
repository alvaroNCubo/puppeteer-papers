using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Tetris.Hex;
using Tetris.Hex.Adapters;

namespace Tetris.HexWeb;

/// <summary>
/// Staging 2's DRIVEN adapter: an <see cref="IBoardOutputPort"/> implementation
/// that broadcasts each presented board, as frame JSON, to the WebSockets
/// connected for a session. The exact counterpart of the journaled example's
/// <c>WebSocketSink</c>, one layer over: that one implements the substrate's
/// <c>IOutputSink</c>, this one implements the port the domain declares.
/// <para>
/// <see cref="Present"/> is called on whatever thread drove the move, so it
/// stamps and enqueues and hands the socket sends to a background pump. It also
/// keeps the last frame, so a socket that attaches mid-game can be shown the
/// current board at once — the hexagon offers no way to ask for it.
/// </para>
/// </summary>
public sealed class WebSocketBoardOutput : IBoardOutputPort, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, WebSocket> sockets = new();
    private readonly Channel<string> outbound =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    private readonly Task pump;
    private volatile string? lastFrame;

    /// <summary>Raised with each frame, so a hub can fan it out to observers too.</summary>
    public event Action<string>? FramePushed;

    public WebSocketBoardOutput() => pump = Task.Run(PumpAsync);

    /// <summary>The most recent frame, or null before the first present.</summary>
    public string? LastFrame => lastFrame;

    /// <summary>Attach a connected socket; returns a token to detach it later.</summary>
    public Guid Attach(WebSocket socket)
    {
        var id = Guid.NewGuid();
        sockets[id] = socket;
        return id;
    }

    public void Detach(Guid id) => sockets.TryRemove(id, out _);

    /// <summary>Send the most recent frame to one socket immediately (e.g. on connect).</summary>
    public async Task SendCurrentTo(WebSocket socket, CancellationToken ct)
    {
        var frame = lastFrame;
        if (frame is not null && socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text, true, ct);
        }
    }

    // IBoardOutputPort: stamp + enqueue; never block the caller that moved.
    public void Present(BoardView board)
    {
        var frame = FrameJson.Of(board);
        lastFrame = frame;
        outbound.Writer.TryWrite(frame);
        FramePushed?.Invoke(frame);
    }

    private async Task PumpAsync()
    {
        await foreach (var frame in outbound.Reader.ReadAllAsync())
        {
            var bytes = Encoding.UTF8.GetBytes(frame);
            foreach (var (id, socket) in sockets)
            {
                if (socket.State != WebSocketState.Open)
                {
                    sockets.TryRemove(id, out _);
                    continue;
                }

                try
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    sockets.TryRemove(id, out _); // dropped socket; the next frame skips it
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        outbound.Writer.TryComplete();
        try { await pump; } catch { }
    }
}
