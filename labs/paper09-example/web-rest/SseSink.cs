using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Puppeteer;

namespace Tetris.WebRest;

/// <summary>
/// An <see cref="IOutputSink"/> that streams each frame's
/// <see cref="PushDocument.Document"/> (the JSON the frame reaction emits) to the
/// session's open Server-Sent-Events responses. It is the exact analogue of the
/// WebSocket lab's WebSocketSink and of FrameFileSink — the OutputTarget seam is
/// the same; only the wire differs (an SSE <c>data:</c> frame instead of a socket
/// message or a file write). SSE is the push mechanism REST lacks natively, so
/// this is where "output" lives as its OWN channel, separate from the POST that
/// carries input.
/// <para>
/// Per the <see cref="IOutputSink"/> contract <see cref="Push"/> must not block —
/// it runs on the reaction thread. It stamps the latest frame and hands the
/// writes to a background pump. It keeps the last frame so a fresh subscriber is
/// shown the current state on connect.
/// </para>
/// </summary>
public sealed class SseSink : IOutputSink, IAsyncDisposable
{
    // Each subscriber is a held-open response body writer.
    private readonly ConcurrentDictionary<Guid, StreamWriter> subscribers = new();
    private readonly Channel<string> outbound =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task pump;
    private volatile string? lastFrame;

    /// <summary>Raised with each pushed frame, so a hub can fan it out to observers.</summary>
    public event Action<string>? FramePushed;

    public SseSink() => pump = Task.Run(PumpAsync);

    /// <summary>The most recent frame pushed, or null before the first push.</summary>
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
