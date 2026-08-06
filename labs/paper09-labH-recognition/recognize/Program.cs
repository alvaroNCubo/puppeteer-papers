using Choreography.Theater;
using Puppeteer;
using Tetris;
using Tetris.Acting;
using Tetris.Recognizing;

// ============================================================================
// Tetris RECOGNIZE — the lab Paper 9 §6 asserts and did not have.
//
// §6 argues that because the domain keeps its identity across stagings, the
// ROUTINE its acts compose is recognizable across stagings too, and concedes
// that no lab in Appendix A recognizes a routine at all. This tool recognizes
// one — the placement of a single piece, spawn through landing — and runs the
// SAME reaction over the journals of two different stagings.
//
// It is additive by construction. Tetris/domain is not touched; no existing
// host is touched. A reaction is declared against a journal from outside both.
//
//   TetrisRecognize play <journalDir> <session> [--gravity]
//       Drive the SAME TetrisActor.Persistent host the warm server uses through
//       a scripted sequence, writing a FileSystem journal at <journalDir>.
//       --gravity lands the first piece by Tick instead of Drop (the control
//       case: a landing the record does not name).
//
//   TetrisRecognize read <journalDir> <session>
//       Open an actor over an EXISTING journal, enumerate the acts it holds,
//       and run the placement reaction over it. This is the verb pointed at the
//       container journals.
//
// <journalDir> is the FileSystem `path=`; the store lives under
// <journalDir>/<session>/ (DiaryStorageFileSystem basePath = path + actor name),
// so `read` over a container's /data with session `tetris` finds /data/tetris.
// ============================================================================

const int width = 10;
const int height = 20;

if (args.Length < 3)
{
    Console.Error.WriteLine(
        "usage: TetrisRecognize play <journalDir> <session> [--gravity]\n" +
        "       TetrisRecognize read <journalDir> <session>");
    return 2;
}

var verb = args[0];
var journalDir = Path.GetFullPath(args[1]);
var session = args[2];

switch (verb)
{
    case "play":
        Play(args.Contains("--gravity"));
        return 0;

    case "read":
        return Read() ? 0 : 1;

    default:
        Console.Error.WriteLine($"unknown verb '{verb}'");
        return 2;
}

// ── play ────────────────────────────────────────────────────────────────────

// The scripted sequence is the one Tetris/sm-cluster/Program.cs plays on the
// three-container staging, verb for verb:
//     spawn, left, rotate, tick, tick, drop      (piece 1 lands on the drop)
//     spawn, right, right, drop                  (piece 2 lands on the drop)
// The piece TYPES are not scriptable from outside the domain — the well chooses
// them itself (well.NextPieceLetter(), a transient RNG that is never journaled;
// only the RESOLVED letter is). So the two stagings share an identical verb
// sequence and differ in the spawn argument, which is what the record is for.
void Play(bool gravity)
{
    Directory.CreateDirectory(journalDir);
    using var game = TetrisActor.Persistent(session, width, height, journalDir);

    if (game.Snapshot().IsAwaitingPiece)
    {
        game.SpawnNext();
    }

    game.MoveLeft();
    game.Rotate();

    if (gravity)
    {
        // The control case. Tick until the piece lands under gravity: the well
        // leaves the "piece is falling" state, but NO act in the journal says so
        // — Well.Land() is private and runs inside Tick(). The record shows a run
        // of Tick()s and nothing that marks which one ended the placement.
        var ticks = 0;
        while (!game.Snapshot().IsAwaitingPiece && !game.Snapshot().IsGameOver && ticks < height + 4)
        {
            game.Tick();
            ticks++;
        }

        Console.WriteLine($"[play] piece 1 landed under gravity after {ticks} Tick()s (no Drop)");
    }
    else
    {
        game.Tick();
        game.Tick();
        game.Drop();
    }

    if (game.Snapshot().IsAwaitingPiece)
    {
        game.SpawnNext();
    }

    game.MoveRight();
    game.MoveRight();
    game.Drop();

    var final = game.Snapshot();
    Console.WriteLine(
        $"[play] session '{session}' journal '{journalDir}' " +
        $"cleared={final.ClearedLines} awaiting={final.IsAwaitingPiece} over={final.IsGameOver} " +
        $"cells={final.Occupied.Count}");
}

// ── read ────────────────────────────────────────────────────────────────────

// Open an actor over the journal that is already there and let the reactions
// read it. The actor issues NO command: the only thing that runs is rehydration
// (the replay the journal is for) and then Reactions.Execute(), a batch sweep
// from checkpoint 0 over the whole history.
bool Read()
{
    var store = Path.Combine(journalDir, session);
    if (!Directory.Exists(store))
    {
        Console.Error.WriteLine($"no journal at {store}");
        return false;
    }

    // Storage FIRST — ConfigureStorage auto-wires the reaction diary. Reactions
    // are defined after Start(), and the output sink is registered before the
    // sweep, or nothing pushed reaches it.
    using var performance = new PerformanceV2(session, typeof(TetrisDomain).Assembly)
        .ConfigureStorage(DatabaseType.FileSystem, $"path={journalDir}")
        .Start();

    var sink = new RecognitionSink();
    performance.OutputTarget(sink);

    var tag = Recognizer.FreshTag();
    var reactions = performance.Actor.Reactions;
    Recognizer.DefineActs(reactions, tag);
    Recognizer.DefinePlacement(reactions, tag);
    Recognizer.DefinePlacementBySpawnToSpawn(reactions, tag);

    reactions.Execute();

    var acts = sink.Rows.Where(r => r.ReactionName.StartsWith("Act_", StringComparison.Ordinal))
        .OrderBy(r => r.ClosingEntryId)
        .ToList();
    var placements = sink.Rows.Where(r => r.ReactionName == "Placement" + tag).ToList();
    var spawnToSpawn = sink.Rows.Where(r => r.ReactionName == "PlacementSpawnToSpawn" + tag).ToList();

    Console.WriteLine($"=== {session} @ {journalDir} ===");

    Console.WriteLine($"ACTS ({acts.Count}) — the record as it stands");
    foreach (var act in acts)
    {
        var verbName = act.ReactionName[4..^tag.Length];
        Console.WriteLine($"  entry {act.ClosingEntryId,4}  {verbName,-9} {act.Detail}");
    }

    Console.WriteLine($"ROUTINE 'placement of one piece'  Spawn($type) -> Drop()   [{placements.Count} recognized]");
    var ordinal = 0;
    foreach (var p in placements)
    {
        Console.WriteLine($"  #{++ordinal}  {p.Detail}  closes at entry {p.ClosingEntryId}");
    }

    Console.WriteLine($"CONTROL 'spawn to next spawn'     Spawn($type) -> Spawn(_) [{spawnToSpawn.Count} recognized]");
    ordinal = 0;
    foreach (var p in spawnToSpawn)
    {
        Console.WriteLine($"  #{++ordinal}  {p.Detail}  closes at entry {p.ClosingEntryId}");
    }

    // A single line a script can diff across stagings without reading the prose:
    // the ordered closing entries of the recognized routine.
    Console.WriteLine(
        "SIGNATURE placements=" + placements.Count +
        " closes=[" + string.Join(",", placements.Select(p => p.ClosingEntryId)) + "]" +
        " pieces=[" + string.Join(",", placements.Select(p => p.Detail)) + "]");

    return true;
}
