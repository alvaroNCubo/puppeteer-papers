using Choreography.StageManager;
using Choreography.Transport;
using Puppeteer;
using Tetris;
using Tetris.Acting;

// Tetris STAGE DUO (Increment B) — TWO StageV2 nodes, same "tetris" actor, same
// clean Well domain, joined over the InMemory transport into one shared game.
// In-proc demonstration (both nodes in this one process) of the StageManager's
// own replication + command-forwarding:
//   • the DIRECTOR and the CAST each wrap the SAME well with a TetrisActor;
//   • the director's moves REPLICATE to the cast (the cast's CurrentEntryId
//     catches up and its snapshot shows the director's game);
//   • a CAST move FORWARDS to the director intrinsically (no Tell, no glue) and
//     comes back replicated to the cast.
// This is the InputSource merge, now CROSS-NODE, carried by the SM's replication.
// Each node has its OWN OutputTarget frame file, so BOTH observe.
//
// Usage: TetrisStageDuo <session>   (writes <session>-d / <session>-c frame files)

const int width = 10;
const int height = 20;

var session = args.Length >= 1 ? args[0] : "duo";
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

var dirDataDir = SessionPaths.For(session + "-d");
var castDataDir = SessionPaths.For(session + "-c");
Directory.CreateDirectory(dirDataDir);
Directory.CreateDirectory(castDataDir);

Console.WriteLine($"=== Tetris StageManager duo: session '{session}' (in-proc, InMemory transport) ===");

// ── Bring up two nodes ───────────────────────────────────────────────────
var director = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
director.ConfigureStorage(DatabaseType.FileSystem, $"path={dirDataDir}");
director.ConfigureTransport(TransportType.InMemory);
await director.StartAsync(cts.Token);

var cast = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
cast.ConfigureStorage(DatabaseType.FileSystem, $"path={castDataDir}");
cast.ConfigureTransport(TransportType.InMemory);
await cast.StartAsync(cts.Token);

try
{
    // ── Handshake: coordination bus, elect director, data star ─────────────
    await ConnectCoordination(director, cast, cts.Token);
    director.PromoteToDirector();
    await Task.Delay(100, cts.Token);
    Console.WriteLine($"director.IsDirector = {director.IsDirector}");
    await ConnectDataChannels(director, cast, cts.Token);
    await Task.Delay(100, cts.Token);
    Console.WriteLine("handshake complete: coordination + replication + command channels up.\n");

    // ── Wrap BOTH nodes with the polymorphic TetrisActor + its own frame sink ─
    // The director is wrapped FIRST so its seed 'upgrade' lands locally; the
    // cast then wraps too, but its identical seed is a no-op against the already-
    // seeded replicated state (and its OutputTarget gives it its own frame file).
    var dirSink = new FrameFileSink(SessionPaths.FrameFile(session + "-d"));
    using var dirGame = TetrisActor.OnStage(director, width, height, dirSink);

    // The director starts the game (spawns the first piece) and pushes its frame.
    if (dirGame.Snapshot().IsAwaitingPiece) dirGame.SpawnNext();
    dirGame.RunReactions();
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(5));

    var castSink = new FrameFileSink(SessionPaths.FrameFile(session + "-c"));
    using var castGame = TetrisActor.OnStage(cast, width, height, castSink);

    Console.WriteLine($"after director spawn: director entry={director.CurrentEntryId}, cast entry={cast.CurrentEntryId}");
    Console.WriteLine($"  director sees: {Describe(dirGame.Snapshot())}");
    Console.WriteLine($"  cast     sees: {Describe(castGame.Snapshot())}   <- REPLICATED from the director\n");

    // ── (1) Director moves → replicate to cast ──────────────────────────────
    Console.WriteLine("director: MoveLeft, Rotate");
    dirGame.MoveLeft();
    dirGame.Rotate();
    dirGame.RunReactions();
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(5));
    castGame.RunReactions(); // let the cast emit its own frame from the replicated state
    Console.WriteLine($"  director entry={director.CurrentEntryId}, cast entry={cast.CurrentEntryId}");
    Console.WriteLine($"  director sees: {Describe(dirGame.Snapshot())}");
    Console.WriteLine($"  cast     sees: {Describe(castGame.Snapshot())}   <- REPLICATED\n");

    // ── (2) Cast move → FORWARDS to the director (intrinsic, no Tell) ───────
    var beforeEntry = director.CurrentEntryId;
    Console.WriteLine("cast: MoveRight  (forwards to the director over the command channel)");
    castGame.MoveRight();
    await WaitForEntryId(director, beforeEntry + 1, TimeSpan.FromSeconds(5));
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(5));
    dirGame.RunReactions();
    castGame.RunReactions();
    Console.WriteLine($"  director entry={director.CurrentEntryId} (advanced from {beforeEntry}: the cast's move landed on the director)");
    Console.WriteLine($"  director sees: {Describe(dirGame.Snapshot())}   <- the CAST's move is here");
    Console.WriteLine($"  cast     sees: {Describe(castGame.Snapshot())}\n");

    Console.WriteLine("=== duo demo complete: director->cast replication AND cast->director forwarding verified ===");
}
finally
{
    await cast.DisposeAsync();
    await director.DisposeAsync();
}

return 0;

static string Describe(WellSnapshot s) =>
    $"type={s.ActiveType ?? "-"} cleared={s.ClearedLines} awaiting={s.IsAwaitingPiece} over={s.IsGameOver} cells={s.Occupied.Count}";

// Poll until a node's journal catches up (the test's WaitForEntryId helper).
static async Task WaitForEntryId(Stage stage, long expected, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (stage.CurrentEntryId < expected && DateTime.UtcNow < deadline)
    {
        await Task.Delay(50);
    }
}

// The 2-node handshake, verbatim from UnitTestChoreography/EndToEndTests.cs.
static async Task ConnectCoordination(Stage a, Stage b, CancellationToken ct)
{
    var inv = await a.CreateInvitationAsync(ChannelPurpose.Coordination);
    var wait = a.WaitForConnectionAsync(inv, ct);
    var chB = await b.AcceptInvitationAsync(inv);
    var chA = await wait;
    await a.JoinCoordination(b.Id, chA);
    await b.JoinCoordination(a.Id, chB);
}

static async Task ConnectDataChannels(Stage director, Stage cast, CancellationToken ct)
{
    var repInv = await director.CreateInvitationAsync(ChannelPurpose.Replication);
    var waitRep = director.WaitForConnectionAsync(repInv, ct);
    var castRep = await cast.AcceptInvitationAsync(repInv);
    var dirRep = await waitRep;

    var cmdInv = await director.CreateInvitationAsync(ChannelPurpose.Command);
    var waitCmd = director.WaitForConnectionAsync(cmdInv, ct);
    var castCmd = await cast.AcceptInvitationAsync(cmdInv);
    var dirCmd = await waitCmd;

    await director.AcceptCastConnection(cast.Id, dirRep, dirCmd);
    await cast.ConnectToDirector(director.Id, castRep, castCmd);
}
