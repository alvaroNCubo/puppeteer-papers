using System;
using System.Collections.Generic;
using System.Text.Json;
using Choreography.StageManager;
using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.Interpreter.Formatters;
using Tetris;

namespace Tetris.Acting;

/// <summary>
/// A typed facade over a Puppeteer host that wraps the clean <see cref="Well"/>
/// domain. The host is polymorphic — a single-actor <see cref="PerformanceV2"/>
/// (<see cref="OnPerformance"/> / the persistent ctors) OR a distributed
/// <see cref="StageV2"/> StageManager (<see cref="OnStage"/>) — behind one set of
/// typed verbs, proving the host is an accidental shell over the same domain.
/// Hosts and runners talk only to these verbs and the typed <see cref="Snapshot"/>;
/// every DSL string lives here, and the host difference is hidden behind
/// <see cref="IGameHost"/>.
/// <para>
/// The well is materialised in actor state by a once-applied <c>upgrade</c>, and
/// each verb formats a small DSL command against that <c>well</c>. The journal
/// records the command stream, so the game is replayable.
/// </para>
/// </summary>
public sealed class TetrisActor : IGameActor
{
    // Check guards (the GENTLE preconditions). CheckThenCommand enacts the command
    // only when the Check condition is true and otherwise leaves state untouched —
    // so in normal play the domain's hard TetrisRuleException is never reached; the
    // domain invariant stays the backstop. "A piece is active" is exactly "not over
    // and not awaiting"; "can spawn" is "awaiting a piece".
    private const string ActivePieceCheck =
        "{ Check(well.IsGameOver == false && well.IsAwaitingPiece == false) WARNING 'no active piece'; }";
    private const string AwaitingPieceCheck =
        "{ Check(well.IsAwaitingPiece == true) WARNING 'not awaiting a piece'; }";

    // The frame projection a reaction Emits on each mutation — scalars, the active
    // piece TYPE (guarded, because well.Active is null while awaiting/over), and the
    // occupied interior cells. Rendered with JsonFormatter on the push channel so a
    // viewer can parse it with System.Text.Json.
    private const string FrameProjection =
        "{ print well.Frame.Width width, well.Frame.Height height, " +
        "well.ClearedLines cleared, well.IsGameOver over, well.IsAwaitingPiece awaiting; " +
        "if (well.IsGameOver == false && well.IsAwaitingPiece == false) { print well.Active.Type type; } " +
        "foreach (cell in well.OccupiedInterior()) { print cell.Row r, cell.Column c; } }";

    // The mutating verbs a frame reaction must fire on. A Job reaction per verb
    // (OR-match within one Seek is not an exercised path), all sharing the same
    // Emit projection. Spawn carries an argument; the rest are nullary.
    private static readonly string[] MutatingVerbs =
        ["Spawn($p)", "MoveLeft()", "MoveRight()", "Rotate()", "Tick()", "Drop()"];

    private readonly IGameHost host;
    private readonly bool pushEnabled;

    // ── Factories ──────────────────────────────────────────────────────────

    /// <summary>
    /// Opens an in-memory <see cref="PerformanceV2"/> session (state lost on process
    /// exit), no push channel. This is the ctor the human console uses — unchanged.
    /// </summary>
    public TetrisActor(string actorName, int width, int height)
        : this(BuildPerformanceHost(actorName, DatabaseType.IN_MEMORY, "InMemory"), width, height, sink: null)
    {
    }

    /// <summary>
    /// Opens a <see cref="PerformanceV2"/> session backed by a persistent FileSystem
    /// journal at <paramref name="journalDirectory"/>. State survives across process
    /// exits (rehydrates by replaying the journal). If <paramref name="sink"/> is
    /// supplied, the frame push channel is wired.
    /// </summary>
    public static TetrisActor Persistent(string actorName, int width, int height, string journalDirectory, IOutputSink? sink = null) =>
        new(BuildPerformanceHost(actorName, DatabaseType.FileSystem, $"path={journalDirectory};maxFileSize=4194304"),
            width, height, sink);

    /// <summary>
    /// Explicit <see cref="PerformanceV2"/> path (alias of the persistent ctor) — the
    /// single-actor host, the symmetric sibling of <see cref="OnStage"/>.
    /// </summary>
    public static TetrisActor OnPerformance(string actorName, int width, int height, string journalDirectory, IOutputSink? sink = null) =>
        Persistent(actorName, width, height, journalDirectory, sink);

    /// <summary>
    /// Wraps an already-started <see cref="StageV2"/> (StageManager) host — the SAME
    /// Well domain over a distributed shell, zero domain changes. The caller owns the
    /// Stage's async lifecycle (StartAsync / handshake / promotion / DisposeAsync);
    /// this actor only drives its verbs. Pass a <paramref name="sink"/> to wire the
    /// frame push channel on this node so it observes (the director and each cast can
    /// each emit frames from the state they hold).
    /// <para>
    /// Note: spawning seeds the well via a command, so a cast (which forwards
    /// commands to the director) must be wrapped only after the handshake; the
    /// director should be wrapped first so the seed lands on it.
    /// </para>
    /// </summary>
    public static TetrisActor OnStage(StageV2 stage, int width, int height, IOutputSink? sink = null) =>
        new(new StageHost(stage), width, height, sink);

    private static IGameHost BuildPerformanceHost(string actorName, DatabaseType storage, string connectionString)
    {
        // The framework discovers the domain (Well, Piece, …) by reflection over the
        // domain assembly; only the public anchor TetrisDomain is needed as the seam.
        var performance = new PerformanceV2(actorName, typeof(TetrisDomain).Assembly)
            .ConfigureStorage(storage, connectionString)
            .Start();
        return new PerformanceHost(performance);
    }

    private TetrisActor(IGameHost host, int width, int height, IOutputSink? sink)
    {
        this.host = host;

        pushEnabled = sink is not null;
        if (sink is not null)
        {
            // Configure the push channel with the JSON formatter (override the TOON
            // default so the frame parses cleanly), then define one Job reaction per
            // mutating verb sharing the frame projection. Company() so every node
            // (director and cast alike) emits frames from the state it holds — the
            // same reaction wiring works on both hosts (Reactions lives at the
            // ActorHandler, below the host topology).
            host.WireOutput(sink, new JsonFormatter());
            foreach (var verb in MutatingVerbs)
            {
                var name = "Frame_" + verb.Split('(')[0];
                host.Reactions.DefineReaction(name)
                    .Job().Company().WithSharedHydration()
                    .Seek(name + "Seek")
                        .OnMatch($"[_:Well].{verb}")
                    .Program.Emit(FrameProjection);
            }
        }

        // Seed the aggregate into actor state. 'upgrade' runs its body once and is
        // recognised as already-applied on every later rehydration — so a persistent
        // (or replicated) session that already has a 'seed' entry keeps its well.
        // The dimensions travel as @params, never interpolated into the text: an
        // Action is a template plus args, and 'seed' stays a literal because it is
        // the upgrade's GUARD NAME, not a value.
        host.Command("upgrade('seed') { well = Well(@width, @height); }",
            p =>
            {
                p["width", typeof(int)] = width;
                p["height", typeof(int)] = height;
            });
    }

    /// <summary>
    /// Drives the frame reactions: replays journal entries appended since the last
    /// run; each that matches a mutating verb has its <c>Program.Emit</c> push the
    /// frame to the sink, SYNCHRONOUSLY (Job/Batch mode). A no-op with no push
    /// channel. Identical on either host (Reactions lives at the ActorHandler).
    /// </summary>
    public void RunReactions()
    {
        if (pushEnabled)
        {
            host.Reactions.Execute();
        }
    }

    // ── Inbound verbs (the controller surface) ─────────────────────────────

    /// <summary>
    /// Spawns the next piece. The piece is chosen by the DOMAIN — a query over
    /// <c>well.NextPieceLetter()</c> (transient RNG, never journaled) — and the
    /// resolved letter is issued as an ACTION ARGUMENT, so the journal holds the
    /// template <c>well.Spawn(@letter);</c> once and this call's letter as its args;
    /// replay rebinds the same letter, which is what makes the transient RNG
    /// deterministic on the way back. The framework coerces the string letter to the
    /// domain's <c>PieceType</c> enum by member name.
    /// </summary>
    public void SpawnNext()
    {
        var letter = QueryString("print well.NextPieceLetter() letter;", "letter");
        // Check-then-command: spawn only when the well is awaiting a piece. The letter
        // travels as an @param, so the journal holds one Action template plus this
        // invocation's argument — and replay re-applies the SAME letter, which is what
        // makes a transient RNG deterministic on the way back.
        Spawn(letter);
    }

    /// <summary>
    /// Spawns the piece named by <paramref name="letter"/> — the deterministic spawn,
    /// for a caller that has already resolved which piece comes next: a script, a
    /// replay, or an experiment feeding two decompositions the same input.
    /// <see cref="SpawnNext"/> is this verb with the domain's own choice.
    /// </summary>
    public void Spawn(string letter) =>
        host.CheckThenCommand(AwaitingPieceCheck, "well.Spawn(@letter);",
            p => { p["letter", typeof(string)] = letter; });

    /// <summary>Slides the active piece one column left (a blocked slide is a no-op).</summary>
    public void MoveLeft() => GuardedVerb("MoveLeft");

    /// <summary>Slides the active piece one column right (a blocked slide is a no-op).</summary>
    public void MoveRight() => GuardedVerb("MoveRight");

    /// <summary>Rotates the active piece one step (a blocked rotation is a no-op).</summary>
    public void Rotate() => GuardedVerb("Rotate");

    /// <summary>Advances the active piece one row; lands it if it cannot descend.</summary>
    public void Tick() => GuardedVerb("Tick");

    /// <summary>Hard-drops the active piece to its resting place and lands it.</summary>
    public void Drop() => GuardedVerb("Drop");

    // Every move verb is check-guarded by the active-piece precondition, so the
    // command runs only while a piece is falling; otherwise it is a clean no-op
    // (state untouched) and the domain's hard guard is never tripped.
    //
    // Each act travels with its own NAME as the invocation's argument. These five
    // acts take no value — there is nothing about "move left" that varies — so the
    // name is what the entry carries, and carrying it is what makes the entry an
    // Action rather than a V1 Script. That distinction is not cosmetic: a Script is
    // invisible to a domain reaction (Reaction.cs, Rule 1), so with a bare
    // "well.MoveLeft();" nothing observes the act and no frame is ever pushed.
    private void GuardedVerb(string act) =>
        host.CheckThenCommand(ActivePieceCheck, $"well.{act}();",
            p => { p["act", typeof(string)] = act; });

    // ── Typed read for rendering + control flow ────────────────────────────

    /// <summary>
    /// Runs queries over the well and parses the results into an immutable
    /// <see cref="WellSnapshot"/> — the only window a host has into game state.
    /// </summary>
    public WellSnapshot Snapshot()
    {
        var scalars = QueryScalars();
        var occupied = QueryCells("well.OccupiedInterior()");

        // OccupiedInterior already includes the falling piece. The active piece's
        // cells and TYPE are queried only when a piece is actually falling
        // (query-first: never touch well.Active while awaiting / over).
        var hasActive = !scalars.IsGameOver && !scalars.IsAwaitingPiece;
        var active = hasActive
            ? QueryCells("well.Active.Cells")
            : (IReadOnlyList<Cell>)Array.Empty<Cell>();

        var activeType = hasActive
            ? QueryString("print well.Active.Type t;", "t")
            : null;

        return new WellSnapshot(
            scalars.Width,
            scalars.Height,
            occupied,
            active,
            scalars.ClearedLines,
            scalars.IsGameOver,
            scalars.IsAwaitingPiece,
            activeType);
    }

    /// <summary>Runs a single-scalar string query and returns the value under <paramref name="key"/>.</summary>
    private string QueryString(string script, string key)
    {
        var json = host.Query(script);
        using var doc = ParseDocument(json);
        return doc.RootElement.GetProperty(key).GetString()
            ?? throw new InvalidOperationException($"Query '{script}' returned no '{key}'.");
    }

    private (int Width, int Height, int ClearedLines, bool IsGameOver, bool IsAwaitingPiece) QueryScalars()
    {
        var json = host.Query(
            "print well.Frame.Width width, well.Frame.Height height, " +
            "well.ClearedLines cleared, well.IsGameOver over, well.IsAwaitingPiece awaiting;");

        using var doc = ParseDocument(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("width").GetInt32(),
            root.GetProperty("height").GetInt32(),
            root.GetProperty("cleared").GetInt32(),
            root.GetProperty("over").GetBoolean(),
            root.GetProperty("awaiting").GetBoolean());
    }

    private IReadOnlyList<Cell> QueryCells(string cellsExpression)
    {
        // A foreach-print loop renders as a JSON array keyed by the LOOP VARIABLE
        // name, each element carrying the print aliases — e.g.
        // {"cell":[{"r":0,"c":3},...]}. An empty loop collapses to "" (-> "{}").
        var json = host.Query(
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

    /// <summary>
    /// The engine collapses an empty document to the empty string; normalise that to
    /// <c>{}</c> so System.Text.Json always has something to parse.
    /// </summary>
    private static JsonDocument ParseDocument(string json) =>
        JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);

    public void Dispose() => host.Dispose();
}
