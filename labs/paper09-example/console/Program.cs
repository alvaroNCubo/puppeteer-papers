using Tetris.Acting;

// An interactive, keyboard-driven Tetris you play in the terminal. It drives a
// TetrisActor — the typed facade over a Puppeteer Performance — and never
// touches the Well or any DSL directly. This is the "console monolith": the host
// supplies everything the domain externalizes — the keyboard (inbound commands),
// the clock (gravity), and the rendering — while the actor turns each call into
// a journaled command. The piece-selection randomness lives in the domain
// (well.NextPieceLetter()); the host just asks the actor to SpawnNext, so the
// whole game flows through the Performance and the journal records the stream.

const int width = 10;
const int height = 20;
var gravityInterval = TimeSpan.FromMilliseconds(500);

// --auto self-plays random moves for a few seconds, non-interactively — a
// headless rendering smoke-test. Without it, the game is keyboard-driven.
var auto = args.Contains("--auto");

// When stdout is redirected (e.g. a headless --auto smoke-test) the cursor APIs
// are unavailable, so we fall back to plain appended frames instead of in-place
// redraw.
var interactiveConsole = !Console.IsOutputRedirected;

// Moves chosen by --auto are deterministic via this seed; the *piece* sequence
// is the domain's own (transient) randomness, captured into the journal.
var moveRandom = new Random(12345);

using var game = new TetrisActor("console", width, height);

// Query-first contract, by construction: the host inspects the snapshot's
// queries (IsGameOver / IsAwaitingPiece) before issuing any operation, so it
// never relies on catching the domain's rule exception.
void SpawnIfAwaiting(WellSnapshot snapshot)
{
    if (snapshot.IsAwaitingPiece)
    {
        game.SpawnNext(); // the actor asks the domain for the next piece
    }
}

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

    var snapshot = game.Snapshot();
    SpawnIfAwaiting(snapshot);            // supply the first piece
    snapshot = game.Snapshot();
    Render(snapshot);

    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8); // only used by --auto
    var nextGravity = DateTime.UtcNow + gravityInterval;

    while (!snapshot.IsGameOver)
    {
        var changed = false;

        if (auto)
        {
            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            changed |= ApplyAutoMove(snapshot);
            Thread.Sleep(40);
        }
        else if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                break;
            }

            changed |= ApplyKey(key, snapshot);
        }

        // Re-read after the input phase: a move/drop may have landed the piece,
        // leaving the well between pieces. Every later decision is query-first
        // against this FRESH snapshot, so we never Tick a pieceless well.
        if (changed)
        {
            snapshot = game.Snapshot();
        }

        // The clock: on each gravity tick the host asks the actor to advance —
        // only while a piece is actually falling (query-first on the fresh state).
        if (DateTime.UtcNow >= nextGravity)
        {
            if (!snapshot.IsAwaitingPiece && !snapshot.IsGameOver)
            {
                game.Tick();
                snapshot = game.Snapshot();
                changed = true;
            }

            nextGravity = DateTime.UtcNow + gravityInterval;
        }

        // A landing leaves the well between pieces; the host feeds the next one.
        if (snapshot.IsAwaitingPiece)
        {
            SpawnIfAwaiting(snapshot);
            snapshot = game.Snapshot();
            changed = true;
        }

        if (changed)
        {
            Render(snapshot);
        }

        if (!auto)
        {
            Thread.Sleep(15); // keep the poll loop from spinning hot
        }
    }

    Render(game.Snapshot()); // final frame (GAME OVER banner if the pile reached the top)
}
finally
{
    if (interactiveConsole)
    {
        Console.CursorVisible = true;
        Console.SetCursorPosition(0, height + 4);
    }
}

// --- input -----------------------------------------------------------------

// Every verb is guarded by a query first — the host only moves when a piece is
// active, so the domain's rule exception is never used for control flow.
bool ApplyKey(ConsoleKey key, WellSnapshot snapshot)
{
    if (snapshot.IsAwaitingPiece || snapshot.IsGameOver)
    {
        return false;
    }

    switch (key)
    {
        case ConsoleKey.LeftArrow: game.MoveLeft(); return true;
        case ConsoleKey.RightArrow: game.MoveRight(); return true;
        case ConsoleKey.UpArrow: game.Rotate(); return true;
        case ConsoleKey.DownArrow: game.Tick(); return true;   // soft drop
        case ConsoleKey.Spacebar: game.Drop(); return true;    // hard drop
        default: return false;
    }
}

bool ApplyAutoMove(WellSnapshot snapshot)
{
    if (snapshot.IsAwaitingPiece || snapshot.IsGameOver)
    {
        return false;
    }

    switch (moveRandom.Next(5))
    {
        case 0: game.MoveLeft(); return true;
        case 1: game.MoveRight(); return true;
        case 2: game.Rotate(); return true;
        case 3: game.Tick(); return true;
        default: game.Drop(); return true;
    }
}

// --- rendering -------------------------------------------------------------

void Render(WellSnapshot snapshot)
{
    // The grid drawing is shared with the AI CLI via BoardRenderer; this host
    // owns only the header text and how the frame reaches the terminal.
    var board = BoardRenderer.Board(
        snapshot,
        "TETRIS — ←/→ move   ↑ rotate   ↓ soft drop   Space hard drop   Q/Esc quit");

    if (interactiveConsole)
    {
        // Cursor-home redraw rather than Console.Clear, to avoid flicker.
        Console.SetCursorPosition(0, 0);
        Console.Write(board);
    }
    else
    {
        // Headless (redirected) fallback: append the frame.
        Console.WriteLine(board);
    }
}
