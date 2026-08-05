using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Puppeteer;

namespace Tetris.Web;

/// <summary>
/// An <see cref="IOutputSink"/> that pushes each frame's
/// <see cref="PushDocument.Document"/> (the JSON the frame reaction emits) to the
/// WebSockets connected for a session — the web analogue of
/// <see cref="Tetris.Acting.FrameFileSink"/>. Instead of overwriting a file, it
/// broadcasts the immutable frame string to every live browser socket. The clean
/// Well is untouched; this is purely the OutputTarget shell.
/// <para>
/// Per the <see cref="IOutputSink"/> contract <see cref="Push"/> must not block —
/// it runs on the reaction's execution thread. So it stamps the latest frame and
/// hands the actual socket sends to a background pump. It also keeps the last
/// frame so a freshly attached socket can be shown the current state at once.
/// </para>
/// </summary>
public sealed class WebSocketSink : IOutputSink, IAsyncDisposable
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

    /// <summary>
    /// Raised with each pushed frame, so a hub can also fan the frame out to
    /// observers watching ALL sessions (the W3 distributed-observation point).
    /// </summary>
    public event Action<string>? FramePushed;

    public WebSocketSink() => pump = Task.Run(PumpAsync);

    /// <summary>The most recent frame pushed, or null before the first push.</summary>
    public string? LastFrame => lastFrame;

    /// <summary>Whether any browser socket is currently attached.</summary>
    public bool HasSockets => !sockets.IsEmpty;

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

    // IOutputSink: called on the reaction thread. Stamp + enqueue; never block.
    public void Push(in PushDocument document)
    {
        lastFrame = document.Document;
        outbound.Writer.TryWrite(document.Document);
        FramePushed?.Invoke(document.Document);
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
