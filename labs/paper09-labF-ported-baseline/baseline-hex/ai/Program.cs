using Tetris.Hex;
using Tetris.Hex.Adapters;
using Tetris.HexAi;

// Staging 4 of the ports-and-adapters baseline: the automated player. One discrete
// operation per process invocation, so an external commander (an AI, a script) can
// play by shelling out one call at a time — the shape of the journaled example's
// Tetris/ai. The commander reads the board through a view it computes for itself
// from the frame file (tools/hex-pile-scan.ps1), never through a rendered grid.
//
// Usage: TetrisHexAi <session> <op>   op ∈ { new, left, right, rotate, tick, drop, view }
//
// This is the staging that reached into the hexagon. Because nothing survives the
// process, the game has to be reloaded on every call, which is what forced a third
// port (IGameStatePort), a second GameService constructor, a Show() verb on the
// driving port, and two seams in the model.

const int width = 10;
const int height = 20;

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "usage: TetrisHexAi <session> <op>   op ∈ { new, left, right, rotate, tick, drop, view }");
    return 2;
}

var session = args[0];
var op = args[1].ToLowerInvariant();

if (op == "new")
{
    if (JsonFileGameState.Exists(session))
    {
        Console.Error.WriteLine(
            $"session '{session}' already has state at {HexSessionPaths.StateFile(session)}; " +
            "pick a fresh session id (never overwrite a game).");
        return 1;
    }
}
else if (!JsonFileGameState.Exists(session))
{
    Console.Error.WriteLine($"unknown session '{session}' — run `new` first.");
    return 1;
}

// ── the composition root: THREE driven adapters in, one driving port out ───────
var screen = new FrameFileBoardOutput(HexSessionPaths.FrameFile(session));
var chooser = new RandomPieceSelection();
var store = new JsonFileGameState();

IGameCommandPort game = new GameService(session, width, height, screen, chooser, store);

switch (op)
{
    case "new":
        game.Start(); // first piece
        break;

    case "left": game.MoveLeft(); break;
    case "right": game.MoveRight(); break;
    case "rotate": game.Rotate(); break;
    case "tick": game.Tick(); break;
    case "drop": game.Drop(); break;

    case "view":
        // Present without moving. The verb exists only because of this op: a fresh
        // process has been presented nothing, and an adapter cannot query the
        // hexagon.
        game.Show();
        break;

    default:
        Console.Error.WriteLine($"unknown op '{op}'   op ∈ {{ new, left, right, rotate, tick, drop, view }}");
        return 2;
}

Console.WriteLine($"session : {session}");
Console.WriteLine($"op      : {op}");
Console.WriteLine($"state   : {HexSessionPaths.StateFile(session)}");
Console.WriteLine($"frame   : {screen.FramePath}");
Console.WriteLine();
Console.WriteLine(File.ReadAllText(screen.FramePath));
return 0;
