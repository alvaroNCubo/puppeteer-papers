using Tetris.Acting;

namespace Tetris.Desktop;

// A native desktop window over the SAME TetrisActor the console drives — ZERO
// domain edits and zero actor edits. Like the console, this host supplies only
// what the domain externalizes: the keyboard (a key press becomes one verb), the
// clock (a ~500 ms gravity Tick on a UI timer) and the drawing (filled cells
// painted from Snapshot()). The rules — walls, pile, rotation, line clears, game
// over, and which piece comes next — all stay in the domain, reached only
// through the actor's typed verbs. The window never sees the Well or any DSL,
// and it could not: this project does not reference the domain, whose one
// public type is an empty anchor.
internal static class Program
{
    private const int Width = 10;
    private const int Height = 20;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // In-memory session, like the console: the game lives as long as the window.
        using var game = new TetrisActor("desktop", Width, Height);
        Application.Run(new GameWindow(game));
    }
}
