using System.Collections.Concurrent;
using System.Threading.Channels;
using Tetris.Hex;
using Tetris.Hex.Adapters;

namespace Tetris.HexWebRest;

/// <summary>
/// Staging 3's DRIVEN adapter: an <see cref="IBoardOutputPort"/> implementation
/// that streams each presented board, as frame JSON, to the session's open
/// Server-Sent-Events responses. The counterpart of the journaled example's
/// <c>SseSink</c>, and the exact analogue of staging 2's
/// <see cref="Tetris.HexWeb.WebSocketBoardOutput"/> — same port, different wire.
/// <para>
/// Over REST the two sides of the hexagon land on physically separate channels:
/// input arrives on a POST, output leaves on this stream. It keeps the last
/// frame for two reasons — to show a fresh subscriber the current board, and to
/// answer the PULL endpoint, since the hexagon exposes no way to ask it anything.
/// </para>
/// </summary>
public sealed class SseBoardOutput : IBoardOutputPort, IAsyncDisposable
{
    // Each subscriber is a held-open response body writer.
    private readonly ConcurrentDictionary<Guid, StreamWriter> subscribers = new();
    private readonly Channel<string> outbound =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task pump;
    private volatile string? lastFrame;

    /// <summary>Raised with each frame, so a hub can fan it out to observers.</summary>
    public event Action<string>? FramePushed;

    public SseBoardOutput() => pump = Task.Run(PumpAsync);

    /// <summary>The most recent frame, or null before the first present.</summary>
    public string? LastFrame => lastFrame;

    /// <summary>Register a subscriber's response writer; returns a token to remove it.</summary>
    public Guid Subscribe(StreamWriter writer)
    {
        var id = Guid.NewGuid();
        subscribers[id] = writer;
        return id;
    }

    public void Unsubscribe(Guid id) => subscribers.TryRemove(id, out _);

    /// <summary>Write one SSE event carrying <paramref name="frame"/> to a single writer.</summary>
    public static async Task WriteEvent(StreamWriter writer, string frame, CancellationToken ct)
    {
        // SSE framing: one "data:" line, terminated by a blank line.
        await writer.WriteAsync($"data: {frame}\n\n");
        await writer.FlushAsync(ct);
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
            foreach (var (id, writer) in subscribers)
            {
                try
                {
                    await WriteEvent(writer, frame, CancellationToken.None);
                }
                catch
                {
                    subscribers.TryRemove(id, out _); // subscriber gone; skip next time
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
