using System.Collections.Concurrent;
using System.Text.Json;

namespace Tetris.HexWebRest;

/// <summary>
/// Owns every session's <see cref="RestGameRoom"/> plus the set of observer SSE
/// writers. Same-id sessions share one hexagon; distinct ids are independent
/// games. Observers are pure consumers of every session's presented frames.
/// </summary>
public sealed class RestGameHub : IDisposable
{
    private readonly ConcurrentDictionary<string, RestGameRoom> rooms = new();
    private readonly ConcurrentDictionary<Guid, StreamWriter> observers = new();

    /// <summary>Get or create the room for a session, wiring its frames to observers.</summary>
    public RestGameRoom Room(string session) =>
        rooms.GetOrAdd(session, id =>
        {
            var room = new RestGameRoom(id);
            room.Output.FramePushed += frame => BroadcastToObservers(id, frame);
            return room;
        });

    public bool TryGet(string session, out RestGameRoom room) => rooms.TryGetValue(session, out room!);

    public Guid AddObserver(StreamWriter writer)
    {
        var oid = Guid.NewGuid();
        observers[oid] = writer;
        return oid;
    }

    public void RemoveObserver(Guid oid) => observers.TryRemove(oid, out _);

    /// <summary>Send an observer the current frame of every active session (on connect).</summary>
    public async Task SendAllCurrentTo(StreamWriter writer, CancellationToken ct)
    {
        foreach (var (id, room) in rooms)
        {
            if (room.Output.LastFrame is { } frame)
            {
                await SseBoardOutput.WriteEvent(writer, Envelope(id, frame), ct);
            }
        }
    }

    private void BroadcastToObservers(string session, string frame)
    {
        if (observers.IsEmpty) return;

        var envelope = Envelope(session, frame);
        foreach (var (oid, writer) in observers)
        {
            var capturedOid = oid;
            // Fire-and-forget; drop the observer if the write faults.
            _ = SseBoardOutput.WriteEvent(writer, envelope, CancellationToken.None)
                .ContinueWith(t => { if (t.IsFaulted) observers.TryRemove(capturedOid, out _); },
                    TaskScheduler.Default);
        }
    }

    // Observer envelope: wrap the session's frame so the page can grid them by id.
    private static string Envelope(string session, string frame) =>
        $"{{\"session\":{JsonSerializer.Serialize(session)},\"frame\":{frame}}}";

    public void Dispose()
    {
        foreach (var room in rooms.Values) room.Dispose();
        rooms.Clear();
        observers.Clear();
    }
}
