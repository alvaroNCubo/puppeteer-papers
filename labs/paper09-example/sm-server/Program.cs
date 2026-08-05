using System.IO.Pipes;
using Choreography.StageManager;
using Puppeteer;
using Tetris;
using Tetris.Acting;

// Tetris STAGE SERVER (Increment A) — the SAME warm game host as the v3
// TetrisServer, but hosted by a distributed StageV2 (StageManager) DIRECTOR
// instead of a single-actor PerformanceV2. ZERO domain changes: the clean Well
// runs under StageManager exactly as under Performance, proving the host is an
// accidental shell. TetrisActor.OnStage is the polymorphic frontier — one actor
// type over either host.
//
// Usage: TetrisStageServer <session>
//   Commands arrive over a named pipe ("tetris-<session>"), one per connection:
//     left | right | rotate | tick | drop | view | quit
//   (so the unchanged TetrisSend client and tetris-watch viewer work over the SM
//    host too). Each applied command pushes the frame to the session frame file.

const int width = 10;
const int height = 20;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: TetrisStageServer <session>");
    return 2;
}

var session = args[0];
var dir = SessionPaths.For(session);
var pipeName = "tetris-" + session;
Directory.CreateDirectory(dir);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ── Bring up a lone StageV2 director ────────────────────────────────────────
// FileSystem storage at the session dir, InMemory transport. A lone director
// needs no data channels — it can PerformCmd immediately after promotion.
var stage = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
stage.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");
stage.ConfigureTransport(TransportType.InMemory);
await stage.StartAsync(cts.Token);
// A lone director is "isolated" (no reachable peer), so promotion must be forced
// — there is no other node to coordinate with in the single-node topology.
stage.PromoteToDirector(force: true);
Console.WriteLine($"TetrisStageServer: StageV2 director up (IsDirector={stage.IsDirector}), session '{session}', pipe '{pipeName}'.");

try
{
    // Wrap the warm director with the polymorphic actor + the push sink. The
    // OnStage path wires OutputTarget + the frame Job reaction and seeds the well —
    // all via the same TetrisActor as the Performance hosts.
    var sink = new FrameFileSink(SessionPaths.FrameFile(session));
    using var game = TetrisActor.OnStage(stage, width, height, sink);

    void SpawnIfAwaiting()
    {
        if (game.Snapshot().IsAwaitingPiece)
        {
            game.SpawnNext();
        }
    }

    // Opening piece + frame (the director starts the game).
    SpawnIfAwaiting();
    game.RunReactions();
    LogState(game, "start");

    // ── Named-pipe command loop (same shape as the v3 TetrisServer) ─────────
    while (!cts.IsCancellationRequested)
    {
        using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        try
        {
            await pipe.WaitForConnectionAsync(cts.Token);
        }
        catch (OperationCanceledException) { break; }
        catch (IOException) { continue; }

        string? command;
        using (var reader = new StreamReader(pipe))
        {
            command = reader.ReadLine();
        }

        if (command is null) continue;
        command = command.Trim().ToLowerInvariant();
        if (command == "quit")
        {
            Console.WriteLine("applied: quit -> shutting down");
            break;
        }

        Apply(game, command, SpawnIfAwaiting);
    }
}
finally
{
    // StageV2 is IAsyncDisposable — tear the director down cleanly.
    await stage.DisposeAsync();
}

Console.WriteLine("TetrisStageServer stopped.");
return 0;

// Same orchestration as Tetris/ai and the v3 server: Check-guarded verbs;
// tick/drop then spawn-if-awaiting; view = read-only. Then push the frame.
static void Apply(TetrisActor game, string command, Action spawnIfAwaiting)
{
    var s = game.Snapshot();
    var active = !s.IsAwaitingPiece && !s.IsGameOver;

    switch (command)
    {
        case "left": if (active) game.MoveLeft(); break;
        case "right": if (active) game.MoveRight(); break;
        case "rotate": if (active) game.Rotate(); break;
        case "tick": if (active) { game.Tick(); spawnIfAwaiting(); } break;
        case "drop": if (active) { game.Drop(); spawnIfAwaiting(); } break;
        case "view": break;
        default: Console.WriteLine($"ignored: unknown command '{command}'"); return;
    }

    game.RunReactions();
    LogState(game, command);
}

static void LogState(TetrisActor game, string label)
{
    var s = game.Snapshot();
    Console.WriteLine(
        $"applied: {label} -> type={s.ActiveType ?? "-"} cleared={s.ClearedLines} " +
        $"awaiting={s.IsAwaitingPiece} over={s.IsGameOver}");
}
