using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Choreography.Theater;
using Choreography.Told;
using Choreography.Transport.Brokered;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Tetris;

namespace Tetris.Acting;

/// <summary>
/// The staging for the RE-CUT domain: two actors, <c>pile</c> and <c>piece</c>,
/// each with its own journal, its own verbs, and its own voice — behind the same
/// <see cref="IGameActor"/> surface the single-<c>Well</c> staging offers. A host
/// pointed at this instead of <see cref="TetrisActor"/> is driving two roles and
/// does not have to know it.
/// <para>
/// The choreography is the whole of the coupling, and it is two utterances per
/// landing and none per move:
/// </para>
/// <list type="number">
/// <item>the piece role's <c>Tick</c>/<c>Drop</c> lands a piece and
/// <c>expose</c>s the cells it rests on onto that very journal entry;</item>
/// <item>a Reaction on the piece role matches the snapshot and
/// <c>tell Landed … to pile</c> — a <c>tell</c> is legal only in a Reaction's
/// Causation body, never from a command, so the command records the fact and the
/// reaction, downstream, speaks about it;</item>
/// <item>the pile role takes it up (<c>Told</c>) with its OWN verb,
/// <c>pile.Absorb(cells)</c>, acks the piece role, and exposes the pile that
/// resulted together with its verdict on game over;</item>
/// <item>a Reaction on the pile role matches THAT and
/// <c>tell Absorbed … to piece</c>; the piece role takes it up with its own verb,
/// <c>piece.Take(pile, over)</c>, and stops settling.</item>
/// </list>
/// <para>
/// There is no synchronous cross-role question anywhere in it, and there is
/// nothing speculative: while a piece falls, the pile cannot change, so the
/// projection the piece role decides against is not a guess about the pile — it is
/// the pile.
/// </para>
/// <para>
/// Scope: one process, both roles local, an in-process broker standing in for the
/// wire. The transport is deliberately the least interesting part — the same
/// wiring over a real broker is a deployment question, and this actor is here to
/// measure the re-cut, not the medium.
/// </para>
/// </summary>
public sealed class SplitTetrisActor : IGameActor
{
    // The pile role's topic and the piece role's topic. The DSL never names them;
    // this table is the deployment's business.
    private const string PileTopic = "tetris-pile-v1";
    private const string PieceTopic = "tetris-piece-v1";

    // Check guards (the GENTLE preconditions), in the piece role's vocabulary. The
    // single-Well staging guarded moves with "not over and not awaiting", which was
    // the same thing as "a piece is falling" only because the well had three
    // states. The piece role has four — settling is the new one — so the guard has
    // to say what it means.
    private const string FallingCheck =
        "{ Check(piece.IsFalling == true) WARNING 'no falling piece'; }";
    private const string AwaitingCheck =
        "{ Check(piece.IsAwaitingPiece == true) WARNING 'not awaiting a piece'; }";

    // A landing is the only thing either role has to say to the other. The piece
    // role's command records the landing; this expose snapshots, onto that same
    // entry, exactly what the utterance will carry — the resting cells and the
    // landing's identity. It is guarded by IsSettling so a tick that merely
    // descended exposes nothing and no utterance follows.
    private const string ExposeLandingIfSettled =
        "if (piece.IsSettling == true) { expose piece.LandedCells cells, piece.LandingToken token; }";

    private readonly PerformanceV2 pile;
    private readonly PerformanceV2 piece;
    private readonly InProcessBroker broker = new();
    private readonly BrokerTellTransport pileTransport;
    private readonly BrokerTellTransport pieceTransport;
    private readonly ToldListener pileListener;
    private readonly ToldListener pieceListener;

    /// <summary>How many settle rounds the last landing took (1 in normal play).</summary>
    public int LastSettleRounds { get; private set; }

    /// <summary>
    /// Opens both roles in memory — state lost on process exit. The cut under test,
    /// with nothing durable to get in the way.
    /// </summary>
    public static SplitTetrisActor InMemory(string name, int width, int height) =>
        new(name, width, height, journalRoot: null);

    /// <summary>
    /// Opens both roles over persistent FileSystem journals under
    /// <paramref name="journalRoot"/> — <c>&lt;root&gt;/pile</c> and
    /// <c>&lt;root&gt;/piece</c>. Two roles, two journals: each records the acts ITS
    /// role performed and no others.
    /// </summary>
    public static SplitTetrisActor Persistent(string name, int width, int height, string journalRoot) =>
        new(name, width, height, journalRoot);

    /// <summary>The pile role's journal directory, or <c>null</c> when in memory.</summary>
    public string? PileJournal { get; }

    /// <summary>The piece role's journal directory, or <c>null</c> when in memory.</summary>
    public string? PieceJournal { get; }

    /// <summary>The pile role's actor name — its journal is nested under it.</summary>
    public string PileActorName { get; }

    /// <summary>The piece role's actor name — its journal is nested under it.</summary>
    public string PieceActorName { get; }

    private SplitTetrisActor(string name, int width, int height, string? journalRoot)
    {
        var bindings = new TellBindingTable()
            .Bind("pile", PileTopic)
            .Bind("piece", PieceTopic);

        if (journalRoot is not null)
        {
            PileJournal = Path.Combine(journalRoot, "pile");
            PieceJournal = Path.Combine(journalRoot, "piece");
            Directory.CreateDirectory(PileJournal);
            Directory.CreateDirectory(PieceJournal);
        }

        PileActorName = $"{name}-pile";
        PieceActorName = $"{name}-piece";
        pile = Open(PileActorName, PileJournal);
        piece = Open(PieceActorName, PieceJournal);

        // Seed each role's own aggregate. 'upgrade' runs its body once and is
        // recognised as already-applied on every later rehydration, so a persistent
        // role keeps the state its journal already holds.
        // The dimensions travel as @params, never interpolated into the text, so each
        // seed journals as an Action rather than a V1 literal Script. 'seed' stays a
        // literal because it is the upgrade's GUARD NAME, not a value.
        pile.Using("upgrade('seed') { pile = PileWell(@width, @height); }")
            .WithParameters(p => { p["width", typeof(int)] = width; p["height", typeof(int)] = height; })
            .PerformCommand();
        piece.Using("upgrade('seed') { piece = PieceWell(@width, @height); }")
            .WithParameters(p => { p["width", typeof(int)] = width; p["height", typeof(int)] = height; })
            .PerformCommand();

        pileTransport = new BrokerTellTransport(broker, bindings, witnessName: "broker");
        pieceTransport = new BrokerTellTransport(broker, bindings, witnessName: "broker");
        pile.UseTellTransport(pileTransport);
        piece.UseTellTransport(pieceTransport);

        // The piece role speaks about a landing it lived. `$cells`/`$token` capture
        // from the exposed snapshot; `@cells`/`@token` reference them in the
        // Causation body. `once @token` makes the utterance idempotent per landing,
        // so a redelivery is acked without being absorbed twice.
        piece.Actor.Reactions.DefineReaction("TellLanded")
            .Job().Company().WithSharedHydration()
            .Seek("Landing")
                .OnMatch("expose $cells cells; expose $token token;")
            .Causation.Continue("tell Landed with @cells, @token to pile once @token;");

        // The pile role speaks about the absorb it performed — the pile that
        // resulted and its verdict on game over, which is the pile role's alone.
        pile.Actor.Reactions.DefineReaction("TellAbsorbed")
            .Job().Company().WithSharedHydration()
            .Seek("Absorption")
                .OnMatch("expose $cells pile; expose $over over; expose $token token;")
            .Causation.Continue("tell Absorbed with @cells, @over, @token to piece once 'absorbed-' + @token;");

        // Uptake: one perform per delivered utterance, in the HEARER's own verb.
        // The pile role hears "a piece landed on these cells" and absorbs them;
        // absorbing is the only thing it can do, and nothing else can do it.
        pileListener = pile
            .ListenAs("pile", bindings, broker)
            .Told("Landed").With<string>("cells").With<string>("token")
                .Command("pile.Absorb(@cells); expose pile.Projection pile, pile.IsGameOver over, @token token;")
            .Start();

        // The piece role hears the pile that resulted and takes it as its new
        // projection. It does not recompute game over — it is told.
        pieceListener = piece
            .ListenAs("piece", bindings, broker)
            .Told("Absorbed").With<string>("cells").With<bool>("over").With<string>("token")
                .Command("piece.Take(@cells, @over);")
            .Start();
    }

    // The last error either role's engine reported. A cross-role uptake runs on the
    // delivery path, where a broker deliberately does NOT let a subscriber's failure
    // tear the wire down — so a refused Absorb surfaces as silence, and the only
    // symptom is a landing that never settles. Capturing the error here is what
    // turns that silence back into a diagnosis.
    private ActorExecutionError? lastFailure;

    /// <summary>The last error either role's engine reported, or <c>null</c>.</summary>
    public string? LastFailure =>
        lastFailure is null
            ? null
            : $"{lastFailure.ActorName}: {lastFailure.Exception.Message} — while running: {lastFailure.Script.Trim()}";

    private PerformanceV2 Open(string actorName, string? journalDirectory)
    {
        var performance = new PerformanceV2(actorName, typeof(TetrisDomain).Assembly);
        performance.Actor.ExecutionFailed += error => lastFailure = error;
        if (journalDirectory is null)
        {
            performance.ConfigureStorage(DatabaseType.IN_MEMORY, actorName);
        }
        else
        {
            performance.ConfigureStorage(
                DatabaseType.FileSystem, $"path={journalDirectory};maxFileSize=4194304");
        }

        return performance.Start();
    }

    // ── Inbound verbs (the same surface, in two roles' vocabularies) ────────

    /// <inheritdoc />
    public void SpawnNext()
    {
        // The piece role owns the selection policy, as the well did: resolve the
        // letter with a query over the transient source, then issue the resolved
        // letter as the Action's ARGUMENT, so replay rebinds the same letter and the
        // transient source never runs again.
        var letter = QueryString(piece, "print piece.NextPieceLetter() letter;", "letter");
        Spawn(letter);
    }

    /// <inheritdoc />
    public void Spawn(string letter) =>
        piece.Using(AwaitingCheck, "piece.Spawn(@letter);")
            .WithParameters(p => { p["letter", typeof(string)] = letter; })
            .PerformCheckThenCommand();

    /// <inheritdoc />
    public void MoveLeft() => GuardedVerb("MoveLeft");

    /// <inheritdoc />
    public void MoveRight() => GuardedVerb("MoveRight");

    /// <inheritdoc />
    public void Rotate() => GuardedVerb("Rotate");

    /// <inheritdoc />
    public void Tick()
    {
        GuardedVerb("Tick", ExposeLandingIfSettled);
        Settle();
    }

    /// <inheritdoc />
    public void Drop()
    {
        GuardedVerb("Drop", ExposeLandingIfSettled);
        Settle();
    }

    // Each act travels with its own NAME as the invocation's argument. These acts
    // take no value — nothing about "move left" varies — so the name is what the
    // entry carries, and carrying it is what makes the entry an Action instead of a
    // V1 literal Script. The piece role's own TellLanded reaction matches the
    // exposed landing rather than the verb, but the entry kind still matters: a
    // Script is one full sentence per call, where an Action is a template written
    // once and one compact argument per act after that.
    private void GuardedVerb(string act, string tail = "") =>
        piece.Using(FallingCheck, $"piece.{act}(); {tail}")
            .WithParameters(p => { p["act", typeof(string)] = act; })
            .PerformCheckThenCommand();

    /// <summary>
    /// Drives the landing choreography to completion: the piece role's Reaction
    /// speaks about the landing, the pile role absorbs and speaks about the pile,
    /// the piece role takes it up. Both Reactions are <c>.Job()</c> so the sweep is
    /// explicit and the whole cycle settles inside this call — deterministic, which
    /// is what a measurement needs. A <c>.Cue()</c> push loop would do exactly this
    /// on its own, at a latency nobody here controls.
    /// <para>
    /// It is a no-op when no landing is pending, so it is safe to call after any
    /// verb.
    /// </para>
    /// </summary>
    public void Settle()
    {
        LastSettleRounds = 0;
        for (var round = 0; round < 4; round++)
        {
            if (!IsSettling())
            {
                return;
            }

            LastSettleRounds++;
            piece.Actor.Reactions.Execute();   // the landing is spoken
            pile.Actor.Reactions.Execute();    // the absorb is spoken back
        }

        if (IsSettling())
        {
            throw new InvalidOperationException(
                "A landing did not settle: the pile role never answered. "
                + (LastFailure ?? "No role reported an error; the choreography itself is broken."));
        }
    }

    /// <inheritdoc />
    public void RunReactions()
    {
        // Both roles' reaction diaries, swept in choreography order. There is no
        // frame push channel here, and that is a finding rather than an omission:
        // a whole-well frame is a join over BOTH roles, and a reaction's Emit is
        // read-only within ONE actor, so neither role can push it. Each could push
        // its own half; joining them is somebody else's job.
        piece.Actor.Reactions.Execute();
        pile.Actor.Reactions.Execute();
    }

    // ── Typed read: one snapshot, joined from two roles ─────────────────────

    /// <inheritdoc />
    public WellSnapshot Snapshot()
    {
        // The pile role is asked for the board's shape, the settled cells, the
        // running count of collapses, and the verdict on game over — all four are
        // its own. The piece role is asked what is falling. Neither knows the whole
        // frame; the join happens here, outside both.
        var pileState = QueryPile();
        var pileCells = QueryCells(pile, "pile.OccupiedInterior()");

        var pieceState = QueryPiece();
        var activeCells = pieceState.IsFalling
            ? QueryCells(piece, "piece.Active.Cells")
            : (IReadOnlyList<Cell>)Array.Empty<Cell>();
        var activeType = pieceState.IsFalling
            ? QueryString(piece, "print piece.Active.Type t;", "t")
            : null;

        var occupied = new List<Cell>(pileCells);
        occupied.AddRange(activeCells);

        return new WellSnapshot(
            pileState.Width,
            pileState.Height,
            occupied,
            activeCells,
            pileState.ClearedLines,
            pileState.IsGameOver,
            pieceState.IsAwaitingPiece,
            activeType);
    }

    /// <summary>
    /// Whether the piece role has landed a piece and not yet been told the pile
    /// that resulted — the state the re-cut created. Exposed because a host driving
    /// two roles can observe it, where a host driving one well could not.
    /// </summary>
    public bool IsSettling() =>
        QueryBool(piece, "print piece.IsSettling settling;", "settling");

    /// <summary>
    /// The piece role's belief about game over, for cross-checking it against the
    /// pile role's verdict — which is the one that counts. In correct play they
    /// agree, because one of them is told by the other.
    /// </summary>
    public bool PieceRoleThinksGameOver() =>
        QueryBool(piece, "print piece.IsGameOver over;", "over");

    private (int Width, int Height, int ClearedLines, bool IsGameOver) QueryPile()
    {
        var json = pile.PerformQry(
            "print pile.Frame.Width width, pile.Frame.Height height, " +
            "pile.ClearedLines cleared, pile.IsGameOver over;");

        using var doc = ParseDocument(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("width").GetInt32(),
            root.GetProperty("height").GetInt32(),
            root.GetProperty("cleared").GetInt32(),
            root.GetProperty("over").GetBoolean());
    }

    private (bool IsFalling, bool IsAwaitingPiece) QueryPiece()
    {
        var json = piece.PerformQry(
            "print piece.IsFalling falling, piece.IsAwaitingPiece awaiting;");

        using var doc = ParseDocument(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("falling").GetBoolean(),
            root.GetProperty("awaiting").GetBoolean());
    }

    private static string QueryString(PerformanceV2 actor, string script, string key)
    {
        using var doc = ParseDocument(actor.PerformQry(script));
        return doc.RootElement.GetProperty(key).GetString()
            ?? throw new InvalidOperationException($"Query '{script}' returned no '{key}'.");
    }

    private static bool QueryBool(PerformanceV2 actor, string script, string key)
    {
        using var doc = ParseDocument(actor.PerformQry(script));
        return doc.RootElement.GetProperty(key).GetBoolean();
    }

    private static IReadOnlyList<Cell> QueryCells(PerformanceV2 actor, string cellsExpression)
    {
        var json = actor.PerformQry(
            $"foreach (cell in {cellsExpression}) {{ print cell.Row r, cell.Column c; }}");

        var cells = new List<Cell>();
        using var doc = ParseDocument(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("cell", out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                cells.Add(new Cell(element.GetProperty("r").GetInt32(), element.GetProperty("c").GetInt32()));
            }
        }

        return cells;
    }

    private static JsonDocument ParseDocument(string json) =>
        JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);

    public void Dispose()
    {
        pieceListener.Dispose();
        pileListener.Dispose();
        pieceTransport.Dispose();
        pileTransport.Dispose();
        piece.Dispose();
        pile.Dispose();
    }
}
