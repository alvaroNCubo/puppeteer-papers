using Tetris.Acting;

// Tetris OBSERVER — DOCUMENTED FALLBACK (pull-poll). The primary live viewer is
// now Tetris/watch (TetrisWatch), which receives the game's frame DIRECTLY over
// the OutputTarget PUSH channel. This poll-pull observer is kept only as the
// fallback for when a push channel is unavailable: a long-lived, foreground,
// READ-ONLY watcher that re-reads the session's snapshot and re-renders on change.
// It never issues a command/verb.
//
// Usage: TetrisObserver <session>
//
// Concurrency model: the AI CLI is a SHORT-LIVED process — it opens the journal,
// applies one op, and exits, releasing the journal. A long-lived actor holds its
// OWN in-memory state from its initial replay and would NOT see another process's
// later appends. So the observer RE-OPENS the journal fresh on every poll
// (rehydrate-and-query), which is lock-safe alongside the writer and always
// reflects the latest persisted state. Verified: a held-open reader sees no lock
// error but goes stale; re-open-per-poll tracks the writer's appends.
//
// PULL vs PUSH (Paper-9): this poll-pull is the floor — a "narrator" that
// reconstructs the board by re-reading the journal. The direct upgrade, now
// realised in Tetris/watch, is OutputTarget PUSH: the game EMITS each frame and
// the viewer RECEIVES it (FileSystemWatcher on the pushed frame file), no polling
// and no journal reconstruction.

const int width = 10;
const int height = 20;
var pollInterval = TimeSpan.FromMilliseconds(350);

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: TetrisObserver <session>");
    return 2;
}

var session = args[0];
var journalDir = SessionPaths.For(session);

if (!Directory.Exists(journalDir))
{
    Console.Error.WriteLine(
        $"session '{session}' has no journal yet at {journalDir}. Start it with `TetrisAi {session} new`.");
    return 1;
}

// Ctrl-C exits cleanly.
var stop = false;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };

// When stdout is redirected (e.g. a headless smoke-test) the cursor APIs are
// unavailable, so we append changed frames instead of in-place redraw.
var interactiveConsole = !Console.IsOutputRedirected;
if (interactiveConsole)
{
    Console.CursorVisible = false;
}

try
{
    if (interactiveConsole)
    {
        Console.Clear();
    }

    string? lastFrame = null;

    while (!stop)
    {
        // Q / Esc also exit (interactive only; a redirected run has no keyboard).
        if (interactiveConsole && Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                break;
            }
        }

        var snapshot = ReadSnapshot();
        if (snapshot is not null)
        {
            var frame = RenderFrame(snapshot);
            if (frame != lastFrame) // repaint only on change — no flicker
            {
                if (interactiveConsole)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.Write(frame);
                }
                else
                {
                    Console.WriteLine(frame);
                }

                lastFrame = frame;
            }

            if (snapshot.IsGameOver)
            {
                // Keep the final frame on screen; nothing more will change.
                break;
            }
        }

        Thread.Sleep(pollInterval);
    }
}
finally
{
    if (interactiveConsole)
    {
        Console.CursorVisible = true;
        Console.SetCursorPosition(0, height + 6);
    }
}

return 0;

// Re-open the journal fresh, rehydrate, snapshot, and release — the read-only,
// lock-safe poll. Returns null on a transient read race (e.g. mid-write), so the
// next poll simply tries again.
WellSnapshot? ReadSnapshot()
{
    try
    {
        using var view = TetrisActor.Persistent(session, width, height, journalDir);
        return view.Snapshot();
    }
    catch
    {
        return null;
    }
}

string RenderFrame(WellSnapshot s)
{
    var hud = s.IsGameOver
        ? "game over"
        : s.IsAwaitingPiece ? "awaiting piece" : $"falling: {s.ActiveType}";
    var board = BoardRenderer.Board(s, $"OBSERVER — session {session} — watching (read-only)   [{hud}]");
    return board + Environment.NewLine + "(Q/Esc/Ctrl-C to quit)";
}
