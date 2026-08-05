using System.IO.Pipes;
using Tetris.Acting;

// Tetris SERVER (v3) — the WARM long-lived game host. It rehydrates the well from
// the journal ONCE at startup and then keeps it in memory, applying each command
// against the warm actor. This is the contrast to TetrisAi (v2), which rehydrates
// the whole well from the journal on EVERY op (cost grows with the journal). Here
// the per-command sender (TetrisSend) is a thin named-pipe client carrying just a
// verb — no game logic, no rehydration — so a command round-trips in milliseconds.
//
// Usage: TetrisServer <session>
//   Commands arrive over a named pipe ("tetris-<session>"), one per connection:
//     left | right | rotate | tick | drop | view | quit
//   Each applied command pushes the frame to the session frame file (the same
//   FrameFileSink the watcher reads), synchronously via RunReactions().

const int width = 10;
const int height = 20;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: TetrisServer <session>");
    return 2;
}

var session = args[0];
var journalDir = SessionPaths.For(session);
var pipeName = "tetris-" + session;

// Fresh session: create its journal dir (mirrors TetrisAi `new`). An existing
// session simply rehydrates from its journal.
var fresh = !Directory.Exists(journalDir) || !Directory.EnumerateFileSystemEntries(journalDir).Any();
Directory.CreateDirectory(journalDir);

// Create the persistent actor ONCE: this single rehydrate-at-startup is the whole
// point — every later command reuses this warm in-memory well.
var sink = new FrameFileSink(SessionPaths.FrameFile(session));
using var game = TetrisActor.Persistent(session, width, height, journalDir, sink);

void SpawnIfAwaiting()
{
    if (game.Snapshot().IsAwaitingPiece)
    {
        game.SpawnNext();
    }
}

if (fresh)
{
    SpawnIfAwaiting();   // first piece, like TetrisAi `new`
    game.RunReactions(); // push the opening frame
}

Console.WriteLine($"TetrisServer warm: session '{session}', pipe '{pipeName}', journal '{journalDir}'. Commands: left|right|rotate|tick|drop|view|quit.");

var stop = false;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };

while (!stop)
{
    // One command per connection (the standard named-pipe request pattern). The
    // server awaits a connection, reads a single line, applies it, pushes, and
    // disconnects — the WELL STAYS IN MEMORY across connections.
    using var pipe = new NamedPipeServerStream(
        pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);

    try
    {
        pipe.WaitForConnection();
    }
    catch (IOException)
    {
        continue; // a client aborted mid-handshake; await the next
    }

    string? command;
    using (var reader = new StreamReader(pipe))
    {
        command = reader.ReadLine();
    }

    if (command is null)
    {
        continue;
    }

    command = command.Trim().ToLowerInvariant();
    if (command == "quit")
    {
        Console.WriteLine("applied: quit -> shutting down");
        break;
    }

    Apply(command);
}

Console.WriteLine("TetrisServer stopped.");
return 0;

// Same orchestration as Tetris/ai/Program.cs: Check-guarded verbs; tick/drop then
// spawn-if-awaiting; view is read-only. Then RunReactions() pushes the frame.
void Apply(string command)
{
    var snapshot = game.Snapshot();
    var active = !snapshot.IsAwaitingPiece && !snapshot.IsGameOver;

    switch (command)
    {
        case "left": if (active) game.MoveLeft(); break;
        case "right": if (active) game.MoveRight(); break;
        case "rotate": if (active) game.Rotate(); break;
        case "tick":
            if (active) { game.Tick(); SpawnIfAwaiting(); }
            break;
        case "drop":
            if (active) { game.Drop(); SpawnIfAwaiting(); }
            break;
        case "view":
            break; // no mutation
        default:
            Console.WriteLine($"ignored: unknown command '{command}'");
            return;
    }

    // Push the resulting frame to the sink (synchronous). For a mutating command
    // the reaction matches the new journal entry; for `view` nothing new was
    // appended, so the frame file already holds the latest frame.
    game.RunReactions();

    var after = game.Snapshot();
    Console.WriteLine(
        $"applied: {command} -> type={after.ActiveType ?? "-"} cleared={after.ClearedLines} " +
        $"awaiting={after.IsAwaitingPiece} over={after.IsGameOver}");
}
