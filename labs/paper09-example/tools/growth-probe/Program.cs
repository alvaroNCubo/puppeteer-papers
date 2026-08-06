using Choreography.Theater;
using Puppeteer;
using Tetris;
using Tetris.Acting;

// Growth probe — measurement apparatus for the domain-growth experiment
// (Tetris/notes/domain-growth-score-and-difficulty.md). Two jobs, and nothing else:
//
//   play  <journalDir> <actor> <width> <height> <maxOps> <seed> [random|flat]
//         Plays a whole game through the ordinary TetrisActor verbs against a
//         persistent FileSystem journal, so the journal it leaves behind is
//         exactly what a real host records: Spawn/MoveLeft/MoveRight/Rotate/Drop.
//         Recorded with WHATEVER build of the domain is compiled in — that is the
//         point: a journal recorded before a rule existed. `flat` steers each
//         piece over the currently lowest column before dropping it, which
//         completes rows often enough to record a game that clears many lines;
//         `random` shuffles blindly. Both use only the actor's existing verbs.
//
//   step  <journalDir> <actor> <width> <height>
//         ONE op per process, the way Tetris/ai does it: rehydrate the journal,
//         apply a single deterministic op (spawn if awaiting; else shuffle the
//         piece over the lowest column; else hard-drop), append, exit. Because the
//         process is fresh each time, the engine's script→Action promotion counter
//         (DEFAULT_PROMOTION_CANDIDATE_THRESHOLD = 10, in-memory per handler) never
//         trips, so a LONG game stays journaled as replayable Scripts — which a
//         single-process `play` game does not (see the notes: a promoted NULLARY
//         action cannot be rehydrated, before or after this experiment). Exits 3
//         once the game is over.
//
//   query <journalDir> <actor> <dslScript>
//         Rehydrates that journal (replaying every recorded act) and runs one raw
//         DSL query against the resulting state, printing the engine's JSON. The
//         script is a command-line argument, so the SAME probe binary can ask for
//         facts the domain did not have when the journal was written — no probe
//         edit between the before and after runs.
//
// It is not a staging and is not in Tetris.sln; it never appears in the ripple counts.

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: TetrisGrowthProbe play  <journalDir> <actor> <width> <height> <maxOps> <seed>");
    Console.Error.WriteLine("       TetrisGrowthProbe query <journalDir> <actor> <dslScript>");
    return 2;
}

var mode = args[0].ToLowerInvariant();
var journalDir = Path.GetFullPath(args[1]);
var actorName = args[2];

switch (mode)
{
    case "play":
        return Play();
    case "step":
        return Step();
    case "query":
        return Query();
    default:
        Console.Error.WriteLine($"unknown mode '{mode}'");
        return 2;
}

int Play()
{
    if (args.Length < 7)
    {
        Console.Error.WriteLine("play needs <width> <height> <maxOps> <seed>");
        return 2;
    }

    var width = int.Parse(args[3]);
    var height = int.Parse(args[4]);
    var maxOps = int.Parse(args[5]);
    var seed = int.Parse(args[6]);
    var flat = args.Length > 7 && args[7].Equals("flat", StringComparison.OrdinalIgnoreCase);

    Directory.CreateDirectory(journalDir);
    var moves = new Random(seed);

    using var game = TetrisActor.Persistent(actorName, width, height, journalDir);

    var snapshot = game.Snapshot();
    var ops = 0;

    while (!snapshot.IsGameOver && ops < maxOps)
    {
        if (snapshot.IsAwaitingPiece)
        {
            game.SpawnNext();
            ops++;
            snapshot = game.Snapshot();
            continue;
        }

        if (flat)
        {
            // Steer the piece over the lowest column, rotating now and then, and
            // hard-drop it. Blind to the piece's geometry (the probe cannot see
            // the domain's internals) — only the emitted cells guide it.
            if (moves.Next(4) == 0)
            {
                game.Rotate();
                ops++;
                snapshot = game.Snapshot();
                if (snapshot.IsAwaitingPiece || snapshot.IsGameOver)
                {
                    continue;
                }
            }

            var target = LowestColumn(snapshot);
            var here = snapshot.Active.Count == 0 ? target : snapshot.Active.Min(c => c.Column);
            for (var step = 0; step < width && here != target; step++)
            {
                if (here < target)
                {
                    game.MoveRight();
                }
                else
                {
                    game.MoveLeft();
                }

                ops++;
                snapshot = game.Snapshot();
                if (snapshot.IsAwaitingPiece || snapshot.IsGameOver || snapshot.Active.Count == 0)
                {
                    break;
                }

                var moved = snapshot.Active.Min(c => c.Column);
                if (moved == here)
                {
                    break; // blocked by a wall: the move was a legal no-op
                }

                here = moved;
            }

            if (!snapshot.IsAwaitingPiece && !snapshot.IsGameOver)
            {
                game.Drop();
                ops++;
                snapshot = game.Snapshot();
            }

            continue;
        }

        // A shuffle then a hard drop: enough play to complete rows in a narrow
        // well, and every op is one of the domain's existing verbs.
        switch (moves.Next(6))
        {
            case 0: game.MoveLeft(); break;
            case 1: game.MoveRight(); break;
            case 2: game.Rotate(); break;
            case 3: game.Tick(); break;
            default: game.Drop(); break;
        }

        ops++;
        snapshot = game.Snapshot();
    }

    Console.WriteLine($"PLAYED ops={ops} width={snapshot.Width} height={snapshot.Height} " +
                      $"cleared={snapshot.ClearedLines} over={snapshot.IsGameOver} awaiting={snapshot.IsAwaitingPiece}");
    Console.WriteLine(BoardRenderer.Board(snapshot, $"probe {actorName}"));
    return 0;
}

int Step()
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("step needs <width> <height>");
        return 2;
    }

    var width = int.Parse(args[3]);
    var height = int.Parse(args[4]);

    Directory.CreateDirectory(journalDir);
    using var game = TetrisActor.Persistent(actorName, width, height, journalDir);

    var snapshot = game.Snapshot();
    if (snapshot.IsGameOver)
    {
        Console.WriteLine($"OVER cleared={snapshot.ClearedLines}");
        return 3;
    }

    string op;
    if (snapshot.IsAwaitingPiece)
    {
        game.SpawnNext();
        op = "spawn";
    }
    else
    {
        var target = LowestColumn(snapshot);
        var here = snapshot.Active.Count == 0 ? target : snapshot.Active.Min(c => c.Column);
        if (here != target)
        {
            if (here < target)
            {
                game.MoveRight();
                op = "right";
            }
            else
            {
                game.MoveLeft();
                op = "left";
            }

            // A blocked slide is a legal no-op in this domain, so a piece that
            // cannot reach the target column would otherwise be shuffled forever:
            // drop it instead of stepping again.
            var moved = game.Snapshot();
            if (!moved.IsAwaitingPiece && !moved.IsGameOver
                && moved.Active.Count > 0 && moved.Active.Min(c => c.Column) == here)
            {
                game.Drop();
                op += "+drop";
            }
        }
        else
        {
            game.Drop();
            op = "drop";
        }
    }

    var after = game.Snapshot();
    Console.WriteLine($"STEP {op} cleared={after.ClearedLines} over={after.IsGameOver} awaiting={after.IsAwaitingPiece}");
    return 0;
}

// The emitted frame folds the falling piece into Occupied, so the pile is
// Occupied minus Active — enough to find the column with the most free space.
static int LowestColumn(WellSnapshot snapshot)
{
    var active = new HashSet<Cell>(snapshot.Active);
    var best = 0;
    var bestTop = -1;
    for (var column = 0; column < snapshot.Width; column++)
    {
        var top = snapshot.Height;
        foreach (var cell in snapshot.Occupied)
        {
            if (cell.Column == column && !active.Contains(cell) && cell.Row < top)
            {
                top = cell.Row;
            }
        }

        if (top > bestTop)
        {
            bestTop = top;
            best = column;
        }
    }

    return best;
}

int Query()
{
    var script = args[3];
    if (!Directory.Exists(journalDir))
    {
        Console.Error.WriteLine($"no journal at {journalDir}");
        return 1;
    }

    // Straight onto the engine rather than through TetrisActor: the actor's typed
    // Snapshot() is a fixed set of facts, and the probe must be able to ask for a
    // fact that did not exist when it was written.
    using var performance = new PerformanceV2(actorName, typeof(TetrisDomain).Assembly)
        .ConfigureStorage(DatabaseType.FileSystem, $"path={journalDir};maxFileSize=4194304")
        .Start();

    Console.WriteLine(performance.Using(script).PerformQuery());
    return 0;
}
