using System.Globalization;
using Choreography.StageManager;
using Choreography.Transport;
using Puppeteer;
using Tetris;
using Tetris.Acting;

// ============================================================================
// Tetris STAGE CLUSTER (Increment C2) — a genuine 3-node, CROSS-CONTAINER
// deployment of the same clean Well domain over real Kestrel TLS.
//
// This is the C2 that sm-duo-tls/Program.cs named in its own header ("de-risk
// the network transport before C2 (Docker cross-machine)"). C1 ran TWO StageV2
// nodes in ONE process over loopback TLS. C2 runs ONE StageV2 PER PROCESS, one
// process per Docker container, joined over the compose bridge network by their
// service-name advertise URLs (https://tetris-<id>:5443/). Three containers =
// three machines (Paper 7 §5.2 fixes the peer count at THREE — the minimum at
// which the no-privileged-node claim is unambiguous).
//
// ZERO DOMAIN CHANGES. As in every other Tetris host, the domain is a parameter:
//   StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly)
// The Well, the TetrisActor, and the StageManager machinery are all unchanged;
// only this host and the docker/ files are new (purely additive — the C2 claim).
//
// TOPOLOGY — a fixed Director star (no rotation; that is Paper 7's concern):
//   • node 'a' is the Director: it promotes, plays a short scripted sequence,
//     and REPLICATES every journal entry to the casts over TLS;
//   • nodes 'b' and 'c' are Casts: they receive the replicated Well live over
//     the peer transport and each emit their own frame from the state they hold.
//
// CROSS-CONTAINER RENDEZVOUS — verified against the framework, NOT fabricated.
// The HTTPS transport (Choreography/Transport/Https/HttpsTransport.cs) has no
// discovery server: a ConnectionInvitation is a plain serialisable value
// (InviterId, Purpose, Address), and Address embeds the inviter's ADVERTISE URL
//   address = "{advertiseUrl}|{localId}|{purpose}|{guid}"
// so an accepter in another container connects to the right service name. What
// cannot be discovered on-wire — the invitation Address, the inviter's
// PerformerId, and each node's self-signed TLS fingerprint (pinned before the
// first TLS connect, symmetrically) — must cross OUT OF BAND. Paper 7's proven
// 3-Docker harness carries exactly this bootstrap over a shared /bootstrap
// volume (its analog to the out-of-band Usher/QR hop). We reuse that mechanism.
// Paper 7 additionally runs an Usher to ASSIGN each node an identity; we do not
// need it — a Stage's identity is just its PerformerId, so PerformerId.New() per
// process suffices, exactly as sm-duo / sm-duo-tls already do.
//
// The peer traffic that actually MOVES the Well — coordination (DirectorAnnounce
// / heartbeats), replication (CueEvent / CueAck), and command forwarding — all
// crosses the network between containers over real TLS. Only the initial
// rendezvous bootstrap uses the shared volume.
//
// Environment (see docker/docker-compose.yml):
//   TETRIS_NODE_ID          (req) this node's id: a | b | c
//   PUPPETEER_LISTEN_URL    (req) Kestrel bind URL, e.g. https://0.0.0.0:5443/
//   PUPPETEER_ADVERTISE_URL (req) URL peers dial, e.g. https://tetris-a:5443/
//   TETRIS_PEERS            (req) comma-separated advertise URLs of the OTHERS
//   TETRIS_SESSION          (req) shared actor/session name (same on all nodes)
//   TETRIS_DIRECTOR_ID      (opt) which node id is the fixed Director (default a)
//   TETRIS_BOOTSTRAP_DIR    (opt) shared rendezvous dir (default /bootstrap)
//   TETRIS_DATA_DIR         (opt) per-node journal + frame dir (default /data)
// ============================================================================

const int width = 10;
const int height = 20;

string nodeId       = RequireEnv("TETRIS_NODE_ID");
string listenUrl    = RequireEnv("PUPPETEER_LISTEN_URL");
string advertiseUrl = RequireEnv("PUPPETEER_ADVERTISE_URL");
string peersRaw     = RequireEnv("TETRIS_PEERS");
string session      = RequireEnv("TETRIS_SESSION");
string directorId   = GetEnv("TETRIS_DIRECTOR_ID")  ?? "a";
string bootstrapRoot = GetEnv("TETRIS_BOOTSTRAP_DIR") ?? "/bootstrap";
string dataDir      = GetEnv("TETRIS_DATA_DIR")      ?? "/data";

string[] peers = peersRaw
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

bool isDirector = string.Equals(nodeId, directorId, StringComparison.Ordinal);
string myHost = HostOf(advertiseUrl);
string bootstrapDir = Path.Combine(bootstrapRoot, Safe(session));

// Ctrl+C / SIGTERM ends the "hold alive" wait cleanly (docker compose down).
using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; lifetime.Cancel(); };
var ct = lifetime.Token;

// The handshake phase has its own deadline so a missing peer fails loudly rather
// than hanging forever; after convergence the node holds alive indefinitely.
using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
handshakeCts.CancelAfter(TimeSpan.FromMinutes(3));
var hct = handshakeCts.Token;

Log($"node={nodeId} role={(isDirector ? "DIRECTOR" : "cast")} listen={listenUrl} advertise={advertiseUrl} " +
    $"peers=[{string.Join(",", peers)}] session={session}");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(bootstrapDir);

try
{
    // ── 1. Bring up this node's Stage over real TLS ─────────────────────────
    // The domain is a parameter; storage is the per-node volume; the transport is
    // Kestrel HTTPS bound on listenUrl and advertised as advertiseUrl.
    var stage = StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly);
    stage.ConfigureStorage(DatabaseType.FileSystem, $"path={dataDir}");
    stage.ConfigureTransport(TransportType.Https, listenUrl, httpsAdvertiseUrl: advertiseUrl);
    await stage.StartAsync(hct);
    Log($"Stage started; local TLS fingerprint {Short(stage.LocalHttpsCertFingerprint)}");

    // The Director owns the coordination + data-star invitations, so it clears
    // any stale ones a previous run of the SAME session left on the shared volume
    // (run-demo.sh also does `docker compose down -v`; this is belt-and-braces so
    // a manual re-`up` cannot make a cast read a dead invitation). Casts never
    // write invitation files, so only the Director cleans.
    if (isDirector)
        foreach (var stale in Directory.EnumerateFiles(bootstrapDir)
                     .Where(f => !Path.GetFileName(f).StartsWith("fp-", StringComparison.Ordinal)))
            TryDelete(stale);

    // ── 2. Symmetric TLS fingerprint exchange (out-of-band barrier) ─────────
    // Each node publishes its self-signed cert fingerprint and pins every peer's
    // before opening any TLS channel. Pinning is symmetric because HTTPS channels
    // are bidirectional: both endpoints dial each other's advertise URL, so both
    // must trust the other's cert (HttpsTransport.ResolveFingerprintForUrl keys by
    // advertise URL).
    WriteAtomic(Path.Combine(bootstrapDir, $"fp-{myHost}.txt"), stage.LocalHttpsCertFingerprint);
    foreach (var peerUrl in peers)
    {
        string peerFp = await WaitForFileAsync(
            Path.Combine(bootstrapDir, $"fp-{HostOf(peerUrl)}.txt"), TimeSpan.FromMinutes(2), hct);
        stage.TrustPeerHttpsFingerprint(peerUrl, peerFp);
        Log($"pinned peer {peerUrl} → fp {Short(peerFp)}");
    }

    // ── 3. Handshake + play — Director drives; casts follow ─────────────────
    if (isDirector)
        await RunDirectorAsync(stage);
    else
        await RunCastAsync(stage);
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    Log("cancelled; shutting down.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[tetris-{nodeId}] FATAL: {ex}");
    return 1;
}

return 0;

// ────────────────────────────────────────────────────────────────────────────
//  Director: invite each cast (coordination, then the replication/command data
//  star), promote, wrap the Well, play a short scripted sequence, then publish
//  its final journal position and hold alive.
// ────────────────────────────────────────────────────────────────────────────
async Task RunDirectorAsync(StageV2 stage)
{
    // 3a. Coordination with each cast. The Director is the inviter for every
    //     channel (a fixed star needs no lexicographic tiebreaker). Starting the
    //     WaitForConnection task binds Kestrel (EnsureStartedAsync) BEFORE the
    //     invitation file appears, so a cast that reads the file always finds the
    //     listener up.
    var coordWaits = new List<(string host, Task<IStageChannel> wait)>();
    foreach (var peerUrl in peers)
    {
        string h = HostOf(peerUrl);
        var inv = await stage.CreateInvitationAsync(ChannelPurpose.Coordination);
        var wait = stage.WaitForConnectionAsync(inv, hct);
        PublishInvitation(h, "coord", inv);
        coordWaits.Add((h, wait));
    }
    foreach (var (h, wait) in coordWaits)
    {
        var channel = await wait;
        await stage.JoinCoordination(channel.RemotePerformerId, channel);
        Log($"coordination up with {h}");
    }

    // 3b. Promote. At least one coordination peer is connected, so the Stage is no
    //     longer Isolated. Promotion broadcasts a DirectorAnnounce over every
    //     coordination channel, which sets directorId on each cast — the signal
    //     each cast's ConnectToDirector waits for before forwarding commands.
    stage.PromoteToDirector();
    await Task.Delay(200, hct);
    Log($"promoted to Director (IsDirector={stage.IsDirector}) over real TLS");

    // 3c. Data star: replication + command channels to each cast.
    var dataWaits = new List<(string host, Task<IStageChannel> rep, Task<IStageChannel> cmd)>();
    foreach (var peerUrl in peers)
    {
        string h = HostOf(peerUrl);
        var repInv = await stage.CreateInvitationAsync(ChannelPurpose.Replication);
        var cmdInv = await stage.CreateInvitationAsync(ChannelPurpose.Command);
        var repWait = stage.WaitForConnectionAsync(repInv, hct);
        var cmdWait = stage.WaitForConnectionAsync(cmdInv, hct);
        await Task.Delay(150, hct);                 // listener-bind grace window
        PublishInvitation(h, "rep", repInv);
        PublishInvitation(h, "cmd", cmdInv);
        dataWaits.Add((h, repWait, cmdWait));
    }
    var castIds = new List<PerformerId>();
    foreach (var (h, repWait, cmdWait) in dataWaits)
    {
        var rep = await repWait;
        var cmd = await cmdWait;
        await stage.AcceptCastConnection(rep.RemotePerformerId, rep, cmd);
        castIds.Add(rep.RemotePerformerId);
        Log($"data star up with {h} (replication+command)");
    }
    // Let every cast's replication receive-loop settle before the first write, so
    // the live broadcast of entry 1 is not lost to a connect-readiness race. This
    // only reduces noise — the explicit catch-up below is the actual guarantee.
    await Task.Delay(500, hct);
    Log("handshake complete over HTTPS: coordination + replication + command channels up.");

    // 3d. Wrap the SAME Well with the polymorphic TetrisActor and play. The seed
    //     'upgrade' lands on the Director and replicates live to the (already
    //     connected) casts. Each mutating verb fires the frame reaction, which
    //     PUSHES the rendered frame to this node's FrameFileSink — the real
    //     push-channel path (now that TetrisActor issues its verbs as V2 Actions;
    //     see notes §caveat 1). RunReactions (inside PlayScriptedSequence) drives it.
    string framePath = Path.Combine(dataDir, $"{Safe(session)}-{nodeId}.frame");
    var sink = new FrameFileSink(framePath);
    using var game = TetrisActor.OnStage(stage, width, height, sink);

    PlayScriptedSequence(game);

    long finalEntry = stage.CurrentEntryId;
    Log($"scripted sequence done; final journal entry = {finalEntry}");
    Log($"director frame pushed → {framePath}");
    Log($"  director sees: {Describe(game.Snapshot())}");

    // 3e. Guarantee gap-free convergence. The framework's ListenReplication drops
    //     out-of-order entries and does NOT auto-recover, so a cast that missed one
    //     live CueEvent (a connect-readiness race) would never catch up. The
    //     framework's own SendCatchUpAsync is the intended repair: it re-sends every
    //     entry from 1, paced (10ms/entry) so it is delivered in order. Entries a
    //     cast already applied are recognised as older and dropped — so this is
    //     idempotent for the casts that kept up and corrective for any that did not.
    foreach (var castId in castIds)
        await stage.SendCatchUpAsync(castId, 0, hct);
    await Task.Delay(500, hct);
    Log($"catch-up sent to {castIds.Count} cast(s) up to entry {finalEntry}");

    // Publish the target so casts know when they have fully converged.
    WriteAtomic(Path.Combine(bootstrapDir, "done.txt"),
        finalEntry.ToString(CultureInfo.InvariantCulture));

    Console.WriteLine(
        $"[tetris-{nodeId}] convergence checkpoint reached: role=DIRECTOR entry={finalEntry} " +
        $"snapshot={Describe(game.Snapshot())}");

    await HoldAliveAsync();
}

// ────────────────────────────────────────────────────────────────────────────
//  Cast: accept the Director's coordination + data-star invitations, wrap the
//  Well, wait for the replicated journal to catch up to the Director's final
//  entry, emit its frame, and hold alive.
// ────────────────────────────────────────────────────────────────────────────
async Task RunCastAsync(StageV2 stage)
{
    // 3a. Coordination: accept the invitation the Director published for this host.
    var coordInv = await ReadInvitationAsync(myHost, "coord", ChannelPurpose.Coordination);
    var coordCh = await stage.AcceptInvitationAsync(coordInv);
    await stage.JoinCoordination(coordInv.InviterId, coordCh);
    Log("coordination up with director");

    // 3b. Data star: accept replication + command, then ConnectToDirector. With a
    //     command channel present, ConnectToDirector blocks until the
    //     DirectorAnnounce (from the Director's promotion) has set directorId, so
    //     the cast is fully wired before it does anything else.
    var repInv = await ReadInvitationAsync(myHost, "rep", ChannelPurpose.Replication);
    var cmdInv = await ReadInvitationAsync(myHost, "cmd", ChannelPurpose.Command);
    var repCh = await stage.AcceptInvitationAsync(repInv);
    var cmdCh = await stage.AcceptInvitationAsync(cmdInv);
    await stage.ConnectToDirector(repInv.InviterId, repCh, cmdCh, hct);
    Log("data star up with director (replication+command); director announced.");

    // 3c. Wrap the SAME Well. The cast's identical seed 'upgrade' forwards to the
    //     Director and is recognised as already-applied (a no-op); its frame sink
    //     lets it emit the replicated state it holds.
    string framePath = Path.Combine(dataDir, $"{Safe(session)}-{nodeId}.frame");
    var sink = new FrameFileSink(framePath);
    using var game = TetrisActor.OnStage(stage, width, height, sink);

    // 3d. Wait for the Director's target and confirm our replicated journal reached
    //     it — the measured convergence, over the network, over TLS.
    string doneRaw = await WaitForFileAsync(Path.Combine(bootstrapDir, "done.txt"),
        TimeSpan.FromMinutes(2), hct);
    long target = long.Parse(doneRaw, CultureInfo.InvariantCulture);
    await WaitForEntryIdAtLeastAsync(stage, target, TimeSpan.FromSeconds(60), hct);

    // Replay the replicated entries: the frame reaction matches each replicated
    // Action and PUSHES this cast's frame to its own sink — its own view, painted
    // from the state it received over the wire.
    game.RunReactions();

    long entry = stage.CurrentEntryId;
    Log($"caught up to entry {entry} (target {target})");
    Log($"cast frame pushed → {framePath}");
    Log($"  cast sees: {Describe(game.Snapshot())}   <- REPLICATED over TLS");

    Console.WriteLine(
        $"[tetris-{nodeId}] convergence checkpoint reached: role=cast entry={entry} " +
        $"snapshot={Describe(game.Snapshot())}");

    await HoldAliveAsync();
}

// A short, deterministic play: two pieces spawned, nudged, and dropped. Every
// mutating verb fires the frame reaction (RunReactions) so the Director's frame
// file tracks the game, and each entry replicates live to the casts.
void PlayScriptedSequence(TetrisActor game)
{
    if (game.Snapshot().IsAwaitingPiece) game.SpawnNext();
    game.RunReactions();

    game.MoveLeft();  game.RunReactions();
    game.Rotate();    game.RunReactions();
    game.Tick();      game.RunReactions();
    game.Tick();      game.RunReactions();
    game.Drop();      game.RunReactions();   // piece 1 lands

    if (game.Snapshot().IsAwaitingPiece) game.SpawnNext();
    game.RunReactions();

    game.MoveRight(); game.RunReactions();
    game.MoveRight(); game.RunReactions();
    game.Drop();      game.RunReactions();   // piece 2 lands
}

// ── Rendezvous helpers (shared /bootstrap volume) ───────────────────────────

// The invitation Address already carries the inviter's advertise URL and a nonce;
// the InviterId is the Director's PerformerId. Both cross as plain files so the
// accepter can reconstruct the ConnectionInvitation on the far side.
void PublishInvitation(string peerHost, string kind, ConnectionInvitation inv)
{
    WriteAtomic(Path.Combine(bootstrapDir, $"{kind}-{peerHost}.addr"), inv.Address);
    WriteAtomic(Path.Combine(bootstrapDir, $"{kind}-{peerHost}.inviter"), inv.InviterId.ToString());
}

async Task<ConnectionInvitation> ReadInvitationAsync(string forHost, string kind, ChannelPurpose purpose)
{
    string addr = await WaitForFileAsync(
        Path.Combine(bootstrapDir, $"{kind}-{forHost}.addr"), TimeSpan.FromMinutes(2), hct);
    string inviterHex = await WaitForFileAsync(
        Path.Combine(bootstrapDir, $"{kind}-{forHost}.inviter"), TimeSpan.FromMinutes(2), hct);
    return new ConnectionInvitation(new PerformerId(Guid.Parse(inviterHex)), purpose, addr);
}

// ── Small helpers ───────────────────────────────────────────────────────────

async Task HoldAliveAsync()
{
    Log("holding the Stage alive (Ctrl+C / docker stop to exit).");
    try { await Task.Delay(Timeout.Infinite, ct); }
    catch (OperationCanceledException) { }
}

static string Describe(WellSnapshot s) =>
    $"type={s.ActiveType ?? "-"} cleared={s.ClearedLines} awaiting={s.IsAwaitingPiece} " +
    $"over={s.IsGameOver} cells={s.Occupied.Count}";

static async Task WaitForEntryIdAtLeastAsync(Stage stage, long expected, TimeSpan timeout, CancellationToken ct)
{
    var deadline = DateTime.UtcNow + timeout;
    while (stage.CurrentEntryId < expected && DateTime.UtcNow < deadline)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(50, ct);
    }
    if (stage.CurrentEntryId < expected)
        throw new TimeoutException($"journal reached {stage.CurrentEntryId}, expected >= {expected} within {timeout}");
}

static async Task<string> WaitForFileAsync(string path, TimeSpan timeout, CancellationToken ct)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(path))
        {
            string content = await File.ReadAllTextAsync(path, ct);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();
        }
        await Task.Delay(150, ct);
    }
    throw new TimeoutException($"file {path} did not appear within {timeout}");
}

static void WriteAtomic(string path, string content)
{
    string tmp = path + ".tmp";
    File.WriteAllText(tmp, content);
    File.Move(tmp, path, overwrite: true);
}

static void TryDelete(string path)
{
    try { File.Delete(path); } catch (IOException) { }
}

static string HostOf(string url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;

static string Short(string? fingerprint) =>
    string.IsNullOrEmpty(fingerprint) ? "(none)"
        : fingerprint.Length <= 16 ? fingerprint : fingerprint[..16] + "…";

static string Safe(string s)
{
    var safe = string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
    return safe.Length == 0 ? "session" : safe;
}

static string RequireEnv(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
        ? v
        : throw new InvalidOperationException($"Required env var {name} is not set");

static string? GetEnv(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

static void Log(string message) =>
    Console.Error.WriteLine($"[tetris] {message}");
