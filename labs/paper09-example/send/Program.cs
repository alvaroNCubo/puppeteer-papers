using System.IO.Pipes;

// Tetris SEND (v3) — the thin per-command client. It carries one verb to the warm
// TetrisServer over a named pipe and exits. No game logic, no rehydration, no
// engine reference — so it round-trips in milliseconds, the v3 contrast to
// TetrisAi (which rehydrates the whole well from the journal on every op).
//
// Usage: TetrisSend <session> <command>     command ∈ { left|right|rotate|tick|drop|view|quit }

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: TetrisSend <session> <command>   command ∈ { left|right|rotate|tick|drop|view|quit }");
    return 2;
}

var session = args[0];
var command = args[1];
var pipeName = "tetris-" + session;

try
{
    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
    pipe.Connect(2000); // ms; fails fast if no server is listening for this session

    using var writer = new StreamWriter(pipe) { AutoFlush = true };
    writer.WriteLine(command);
}
catch (TimeoutException)
{
    Console.Error.WriteLine($"no server for session '{session}' — start TetrisServer {session} first.");
    return 1;
}

return 0;
