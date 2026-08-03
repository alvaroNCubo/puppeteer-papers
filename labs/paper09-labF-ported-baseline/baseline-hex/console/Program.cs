using Tetris.Hex;
using Tetris.Hex.Adapters;
using Tetris.HexConsole;

// Staging 1 of the ports-and-adapters baseline: an interactive, keyboard-driven
// Tetris in the terminal, over the SAME rules as Tetris/domain (the model files
// in baseline-hex/domain/model differ from theirs only in their namespace line).
//
// This file is the composition root plus the DRIVING adapter. It constructs the
// two driven adapters, hands them to the hexagon, and then does one thing: turn
// key presses into calls on IGameCommandPort. It never touches the Well, and it
// never renders — rendering happens when the hexagon presents through the output
// port, which is the whole point of the arrangement.

const int width = 10;
const int height = 20;
var gravityInterval = TimeSpan.FromMilliseconds(500);

// --auto self-plays random moves for a few seconds, non-interactively — a
// headless smoke-test, the same affordance the journaled console has.
var auto = args.Contains("--auto");
var interactiveConsole = !Console.IsOutputRedirected;
var moveRandom = new Random(12345);

// ── the composition root: two driven adapters in, one driving port out ────────
var screen = new ConsoleBoardOutput(
    interactiveConsole,
    "TETRIS (hex) — ←/→ move   ↑ rotate   ↓ soft drop   Space hard drop   Q/Esc quit");
var chooser = new RandomPieceSelection();

IGameCommandPort game = new GameService(width, height, screen, chooser);

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

    game.Start(); // first piece + opening frame, both presented through the port

    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8); // only used by --auto
    var nextGravity = DateTime.UtcNow + gravityInterval;

    // The driving loop reads game-over off the last VIEW it was presented, not
    // off the domain: an adapter has no way to ask the hexagon anything.
    while (screen.Latest is { IsGameOver: false })
    {
        if (auto)
        {
            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            ApplyAutoMove();
            Thread.Sleep(40);
        }
        else if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                break;
            }

            ApplyKey(key);
        }

        if (DateTime.UtcNow >= nextGravity)
        {
            game.Tick(); // gentle: a no-op if no piece is falling
            nextGravity = DateTime.UtcNow + gravityInterval;
        }

        if (!auto)
        {
            Thread.Sleep(15); // keep the poll loop from spinning hot
        }
    }
}
finally
{
    if (interactiveConsole)
    {
        Console.CursorVisible = true;
        Console.SetCursorPosition(0, height + 4);
    }
}

// --- the driving adapter proper: key -> driving-port call --------------------

void ApplyKey(ConsoleKey key)
{
    switch (key)
    {
        case ConsoleKey.LeftArrow: game.MoveLeft(); break;
        case ConsoleKey.RightArrow: game.MoveRight(); break;
        case ConsoleKey.UpArrow: game.Rotate(); break;
        case ConsoleKey.DownArrow: game.Tick(); break;   // soft drop
        case ConsoleKey.Spacebar: game.Drop(); break;    // hard drop
    }
}

void ApplyAutoMove()
{
    switch (moveRandom.Next(5))
    {
        case 0: game.MoveLeft(); break;
        case 1: game.MoveRight(); break;
        case 2: game.Rotate(); break;
        case 3: game.Tick(); break;
        default: game.Drop(); break;
    }
}
