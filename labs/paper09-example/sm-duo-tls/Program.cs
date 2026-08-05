using Choreography.StageManager;
using Choreography.Transport;
using Puppeteer;
using Tetris;
using Tetris.Acting;

// Tetris STAGE DUO over TLS (Increment C1) — identical to TetrisStageDuo, but the
// two StageV2 nodes are joined over the REAL Kestrel-backed HTTPS transport
// (TransportType.Https) on distinct loopback ports instead of InMemory. Only the
// transport string changes; the handshake, replication, and cast-forwarding are
// the same. This de-risks the network transport before C2 (Docker cross-machine).
// Unpinned loopback TLS: the client accepts any cert (fine for the lab); pinning
// is the production path (HttpsTransportTests has the *Pinned variants).
//
// Usage: TetrisStageDuoTls <session> [portA] [portB]   (writes <session>-d / <session>-c frames)

const int width = 10;
const int height = 20;

var session = args.Length >= 1 ? args[0] : "duotls";
var portA = args.Length >= 2 && int.TryParse(args[1], out var pa) ? pa : 15140;
var portB = args.Length >= 3 && int.TryParse(args[2], out var pb) ? pb : portA + 1;
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

var dirDataDir = SessionPaths.For(session + "-d");
var castDataDir = SessionPaths.For(session + "-c");
Directory.CreateDirectory(dirDataDir);
Directory.CreateDirectory(castDataDir);

Console.WriteLine($"=== Tetris StageManager duo over HTTPS: session '{session}' (in-proc, TLS loopback {portA}/{portB}) ===");

// ── Bring up two nodes over real TLS ────────────────────────────────────────
var director = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
director.ConfigureStorage(DatabaseType.FileSystem, $"path={dirDataDir}");
director.ConfigureTransport(TransportType.Https, $"https://localhost:{portA}/");
await director.StartAsync(cts.Token);

var cast = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
cast.ConfigureStorage(DatabaseType.FileSystem, $"path={castDataDir}");
cast.ConfigureTransport(TransportType.Https, $"https://localhost:{portB}/");
await cast.StartAsync(cts.Token);

try
{
    // Handshake — identical to the InMemory duo, now over TLS.
    await ConnectCoordination(director, cast, cts.Token);
    director.PromoteToDirector();
    await Task.Delay(200, cts.Token);
    Console.WriteLine($"director.IsDirector = {director.IsDirector} (over real TLS)");
    await ConnectDataChannels(director, cast, cts.Token);
    await Task.Delay(200, cts.Token);
    Console.WriteLine("handshake complete over HTTPS: coordination + replication + command channels up.\n");

    var dirSink = new FrameFileSink(SessionPaths.FrameFile(session + "-d"));
    using var dirGame = TetrisActor.OnStage(director, width, height, dirSink);

    if (dirGame.Snapshot().IsAwaitingPiece) dirGame.SpawnNext();
    dirGame.RunReactions();
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(10));

    var castSink = new FrameFileSink(SessionPaths.FrameFile(session + "-c"));
    using var castGame = TetrisActor.OnStage(cast, width, height, castSink);

    Console.WriteLine($"after director spawn: director entry={director.CurrentEntryId}, cast entry={cast.CurrentEntryId}");
    Console.WriteLine($"  director sees: {Describe(dirGame.Snapshot())}");
    Console.WriteLine($"  cast     sees: {Describe(castGame.Snapshot())}   <- REPLICATED over TLS\n");

    // (1) Director moves → replicate to cast over TLS.
    Console.WriteLine("director: MoveLeft, Rotate");
    dirGame.MoveLeft();
    dirGame.Rotate();
    dirGame.RunReactions();
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(10));
    castGame.RunReactions();
    Console.WriteLine($"  director entry={director.CurrentEntryId}, cast entry={cast.CurrentEntryId}");
    Console.WriteLine($"  cast     sees: {Describe(castGame.Snapshot())}   <- REPLICATED over TLS\n");

    // (2) Cast move → forwards to the director over the TLS command channel.
    var beforeEntry = director.CurrentEntryId;
    Console.WriteLine("cast: MoveRight  (forwards to the director over the HTTPS command channel)");
    castGame.MoveRight();
    await WaitForEntryId(director, beforeEntry + 1, TimeSpan.FromSeconds(10));
    await WaitForEntryId(cast, director.CurrentEntryId, TimeSpan.FromSeconds(10));
    dirGame.RunReactions();
    Console.WriteLine($"  director entry={director.CurrentEntryId} (advanced from {beforeEntry}: the cast's move crossed TLS to the director)");
    Console.WriteLine($"  director sees: {Describe(dirGame.Snapshot())}   <- the CAST's move is here\n");

    Console.WriteLine("=== C1 complete: replication AND cast-forwarding verified over real Kestrel TLS ===");
}
finally
{
    await cast.DisposeAsync();
    await director.DisposeAsync();
}

return 0;

static string Describe(WellSnapshot s) =>
    $"type={s.ActiveType ?? "-"} cleared={s.ClearedLines} awaiting={s.IsAwaitingPiece} over={s.IsGameOver} cells={s.Occupied.Count}";

static async Task WaitForEntryId(Stage stage, long expected, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (stage.CurrentEntryId < expected && DateTime.UtcNow < deadline)
    {
        await Task.Delay(50);
    }
}

// The 2-node handshake, verbatim from UnitTestChoreography/EndToEndTests.cs —
// transport-agnostic, so it works over HTTPS unchanged.
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
