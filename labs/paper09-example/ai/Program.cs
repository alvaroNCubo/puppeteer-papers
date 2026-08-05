using Tetris.Acting;

// Tetris AI CLI — one discrete op per process invocation, so an external
// commander (e.g. an AI) can play by shelling out one call at a time. State
// lives in a PERSISTENT FileSystem journal keyed by <session>: each call
// rehydrates from the journal, applies the op, appends to it, and exits. The
// gravity orchestration mirrors Tetris/console/Program.cs exactly — the only
// difference is the clock: the commander sends `tick` instead of a wall-clock.
//
// Usage: TetrisAi <session> <op>     op ∈ { new, left, right, rotate, tick, drop, view }

const int width = 10;
const int height = 20;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: TetrisAi <session> <op>   op ∈ { new, left, right, rotate, tick, drop, view }");
    return 2;
}

var session = args[0];
var op = args[1].ToLowerInvariant();
var journalDir = SessionPaths.For(session);

if (op == "new")
{
    if (Directory.Exists(journalDir) && Directory.EnumerateFileSystemEntries(journalDir).Any())
    {
        Console.Error.WriteLine(
            $"session '{session}' already has a journal at {journalDir}; pick a fresh session id (never overwrite a game).");
        return 1;
    }

    Directory.CreateDirectory(journalDir);
}
else if (!Directory.Exists(journalDir))
{
    Console.Error.WriteLine($"unknown session '{session}' — run `new` first.");
    return 1;
}

// Wire the PUSH channel: each mutating op's frame is emitted by a reaction and
// pushed to this per-session frame file. The live viewer (tetris-watch) watches
// that file and repaints on change — direct push, not a poll.
var sink = new FrameFileSink(SessionPaths.FrameFile(session));
using var game = TetrisActor.Persistent(session, width, height, journalDir, sink);

// Query-first orchestration, mirroring the console: feed a piece whenever the
// well is awaiting one (after a landing or at game start).
void SpawnIfAwaiting()
{
    if (game.Snapshot().IsAwaitingPiece)
    {
        game.SpawnNext();
    }
}

var snapshot = game.Snapshot();
switch (op)
{
    case "new":
        SpawnIfAwaiting();           // first piece
        break;

    case "left":
        if (Active(snapshot)) game.MoveLeft();
        break;
    case "right":
        if (Active(snapshot)) game.MoveRight();
        break;
    case "rotate":
        if (Active(snapshot)) game.Rotate();
        break;

    case "tick":
        // Gravity step: advance one row if a piece is falling; a Tick that lands
        // the piece leaves the well awaiting, so feed the next one — exactly the
        // console's gravity branch + spawn-if-awaiting.
        if (Active(snapshot))
        {
            game.Tick();
            SpawnIfAwaiting();
        }
        break;

    case "drop":
        if (Active(snapshot))
        {
            game.Drop();
            SpawnIfAwaiting();
        }
        break;

    case "view":
        break;                       // read-only

    default:
        Console.Error.WriteLine($"unknown op '{op}'. ops: new, left, right, rotate, tick, drop, view");
        return 2;
}

// Drive the frame reactions: any journal entry this op appended is replayed and
// its frame is Emitted + pushed to the sink SYNCHRONOUSLY, before this short-lived
// process exits. (view appends nothing → no push; the frame file already holds
// the latest frame for the watcher.)
game.RunReactions();

// Always print the commander's projection: the shared board plus a metadata line
// carrying everything needed to choose the next move.
Render(game.Snapshot());
return 0;

static bool Active(WellSnapshot s) => !s.IsAwaitingPiece && !s.IsGameOver;

void Render(WellSnapshot s)
{
    Console.WriteLine(BoardRenderer.Board(s, $"TETRIS (AI) — session {session}"));

    var active = string.Join(" ", s.Active.Select(c => $"({c.Row},{c.Column})"));
    Console.WriteLine(
        $"META type={s.ActiveType ?? "-"} cleared={s.ClearedLines} " +
        $"awaiting={s.IsAwaitingPiece} over={s.IsGameOver} active=[{active}]");
}
