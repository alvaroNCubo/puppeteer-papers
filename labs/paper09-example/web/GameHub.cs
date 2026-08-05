using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Tetris.Web;

/// <summary>
/// Owns every session's <see cref="GameRoom"/> and the set of observer sockets.
/// Players attach to one room (W1/W2); observers watch ALL rooms at once (W3).
/// <list type="bullet">
/// <item>Shared room (W2 scenario 1): several players on the SAME session id share
/// one Well — their moves merge through the room's serial actor.</item>
/// <item>Per-user (W2 scenario 2): distinct session ids are independent games.
/// Both fall out of the one dictionary — no special code.</item>
/// </list>
/// The observer is a pure OutputTarget consumer: one observer, N games — the
/// journal scales with views, not observers.
/// </summary>
public sealed class GameHub : IDisposable
{
    private readonly ConcurrentDictionary<string, GameRoom> rooms = new();
    private readonly ConcurrentDictionary<Guid, WebSocket> observers = new();

    /// <summary>Get or create the room for a session, wiring its frames to observers.</summary>
    public GameRoom Room(string session) =>
        rooms.GetOrAdd(session, id =>
        {
            var room = new GameRoom(id);
            // Fan every frame of this session out to all observers, tagged by id.
            room.Sink.FramePushed += frame => BroadcastToObservers(id, frame);
            return room;
        });

    public IReadOnlyCollection<string> Sessions => (IReadOnlyCollection<string>)rooms.Keys;

    public Guid AttachObserver(WebSocket socket)
    {
        var oid = Guid.NewGuid();
        observers[oid] = socket;
        return oid;
    }

    public void DetachObserver(Guid oid) => observers.TryRemove(oid, out _);

    /// <summary>Send an observer the current frame of every active session (on connect).</summary>
    public async Task SendAllCurrentTo(WebSocket socket, CancellationToken ct)
    {
        foreach (var (id, room) in rooms)
        {
            var frame = room.Sink.LastFrame;
            if (frame is not null)
            {
                await SendEnvelope(socket, id, frame, ct);
            }
        }
    }

    // An observer envelope wraps the session's frame so the page can grid them by id.
    private void BroadcastToObservers(string session, string frame)
    {
        if (observers.IsEmpty) return;

        var envelope = Envelope(session, frame);
        var bytes = Encoding.UTF8.GetBytes(envelope);
        foreach (var (oid, socket) in observers)
        {
            if (socket.State != WebSocketState.Open)
            {
                observers.TryRemove(oid, out _);
                continue;
            }

            // Fire-and-forget; a dropped observer is removed on the next failure.
            var capturedOid = oid;
            _ = socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                .ContinueWith(t => { if (t.IsFaulted) observers.TryRemove(capturedOid, out _); },
                    TaskScheduler.Default);
        }
    }

    private static async Task SendEnvelope(WebSocket socket, string session, string frame, CancellationToken ct)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(Envelope(session, frame)),
                WebSocketMessageType.Text, true, ct);
        }
    }

    private static string Envelope(string session, string frame) =>
        $"{{\"session\":{JsonSerializer.Serialize(session)},\"frame\":{frame}}}";

    public void Dispose()
    {
        foreach (var room in rooms.Values) room.Dispose();
        rooms.Clear();
        observers.Clear();
    }
}
