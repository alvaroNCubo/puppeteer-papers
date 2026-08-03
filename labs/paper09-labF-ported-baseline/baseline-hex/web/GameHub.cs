using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Tetris.HexWeb;

/// <summary>
/// Owns every session's <see cref="GameRoom"/> and the set of observer sockets.
/// Players attach to one room; observers watch every room at once. Same-id
/// sessions share one hexagon; distinct ids are independent games — both fall
/// out of the one dictionary.
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
            room.Output.FramePushed += frame => BroadcastToObservers(id, frame);
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
            if (room.Output.LastFrame is { } frame && socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(Envelope(id, frame)),
                    WebSocketMessageType.Text, true, ct);
            }
        }
    }

    private void BroadcastToObservers(string session, string frame)
    {
        if (observers.IsEmpty) return;

        var bytes = Encoding.UTF8.GetBytes(Envelope(session, frame));
        foreach (var (oid, socket) in observers)
        {
            if (socket.State != WebSocketState.Open)
            {
                observers.TryRemove(oid, out _);
                continue;
            }

            var capturedOid = oid;
            // Fire-and-forget; a dropped observer is removed on the next failure.
            _ = socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None)
                .ContinueWith(t => { if (t.IsFaulted) observers.TryRemove(capturedOid, out _); },
                    TaskScheduler.Default);
        }
    }

    // An observer envelope wraps the session's frame so the page can grid them by id.
    private static string Envelope(string session, string frame) =>
        $"{{\"session\":{JsonSerializer.Serialize(session)},\"frame\":{frame}}}";

    public void Dispose()
    {
        foreach (var room in rooms.Values) room.Dispose();
        rooms.Clear();
        observers.Clear();
    }
}
