using Tetris.Acting;

// Tetris WATCH — a live, foreground, READ-ONLY viewer driven by the OutputTarget
// PUSH channel. It is a DIRECT receiver: the game EMITS each frame (a reaction's
// Program.Emit, fired as the AI CLI applies an op) and pushes it to the session's
// frame file; this viewer watches that file with a FileSystemWatcher and prints
// the new frame the instant it arrives. It renders the EMITTED projection — never
// re-querying or replaying the journal — so it is a viewer of the game's own
// frame, not a narrator reconstructing the board.
//
// Rendering is plain Console.WriteLine: frames stream (the latest is the one at
// the bottom). In-place redraws via Console.Clear / Console.SetCursorPosition did
// NOT display on some terminals (black screen), so the viewer uses the one output
// primitive that works everywhere (cmd, PowerShell, redirected).
//
// Usage: TetrisWatch <session>

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: TetrisWatch <session>");
    return 2;
}

var session = args[0];
var framePath = SessionPaths.FrameFile(session);
var frameDir = Path.GetDirectoryName(framePath)!;
var frameName = Path.GetFileName(framePath);
Directory.CreateDirectory(frameDir);

var stop = false;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };

// Console.KeyAvailable throws when output/input is redirected; guard it.
var interactiveConsole = !Console.IsOutputRedirected;

// Event-driven repaint: a FileSystemWatcher signals when the frame file changes;
// the loop wakes, debounces a burst of writes, reads the latest frame, prints it.
using var signal = new ManualResetEventSlim(false);
using var watcher = new FileSystemWatcher(frameDir, frameName)
{
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
    EnableRaisingEvents = true,
};
watcher.Changed += (_, _) => signal.Set();
watcher.Created += (_, _) => signal.Set();
watcher.Renamed += (_, _) => signal.Set();

Console.WriteLine($"WATCHING session '{session}' (live push, read-only) - Q/Esc/Ctrl-C to quit.");
Console.WriteLine("Waiting for frames... (each move the AI makes prints a new board below)");
Console.WriteLine($"[diag] framePath={framePath}");
Console.WriteLine($"[diag] frameExists={File.Exists(framePath)}  outputRedirected={Console.IsOutputRedirected}");

string? lastFrame = null;
Repaint(ref lastFrame); // print whatever frame already exists

while (!stop)
{
    if (interactiveConsole && Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape)
        {
            break;
        }
    }

    if (signal.Wait(TimeSpan.FromMilliseconds(200)))
    {
        signal.Reset();
        Thread.Sleep(40); // debounce a burst of rapid writes (e.g. land+spawn)
        Repaint(ref lastFrame);
    }
}

return 0;

void Repaint(ref string? lastFrame)
{
    var document = ReadFrame();
    if (document is null || document == lastFrame)
    {
        return;
    }

    lastFrame = document;

    var snapshot = FrameDocument.Parse(document);
    if (snapshot is null)
    {
        return; // a half-written or empty frame; the next push will redraw
    }

    var hud = snapshot.IsGameOver
        ? "game over"
        : snapshot.IsAwaitingPiece ? "awaiting piece" : $"falling: {snapshot.ActiveType}";
    var board = BoardRenderer.Board(snapshot, $"WATCHING session '{session}' (live push)   [{hud}]");

    Console.WriteLine();
    Console.Write(board);
}

// Read the pushed frame document; tolerate transient read races with the sink's
// atomic move (return null, the next change re-triggers).
string? ReadFrame()
{
    try
    {
        return File.Exists(framePath) ? File.ReadAllText(framePath) : null;
    }
    catch (IOException)
    {
        return null;
    }
}
