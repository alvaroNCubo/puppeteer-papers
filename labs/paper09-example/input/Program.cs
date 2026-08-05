using Tetris.Input;

// Tetris STAGE — an actor animated by a MERGE of named INPUT SOURCES (the input
// seam, symmetric sibling of OutputTarget). One warm TetrisActor; several
// IInputSource feed a single serial command channel that the automaton drains
// one command at a time. The medium is a property of each source; the logical
// command (and the pushed frame) are medium-agnostic.
//
// Usage:
//   TetrisStage <session> [--sources keyboard,clock,pipe] [--clock-ms 500]
//
//   Human real-time : TetrisStage g1 --sources keyboard,clock --clock-ms 500
//   AI real-time    : TetrisStage g1 --sources pipe,clock --clock-ms 5000
//                     (drive moves with `TetrisSend g1 <key>`; gravity ticks
//                      autonomously; frames push to the shared frame file for
//                      tetris-watch)
//   Flex (co-drive) : TetrisStage g1 --sources keyboard,pipe,clock --clock-ms 800
//
// Stop: send `quit` from any source (keyboard Q/Esc, or `TetrisSend <session>
// quit`), or Ctrl-C.

if (args.Length < 1)
{
    Console.Error.WriteLine(
        "usage: TetrisStage <session> [--sources keyboard,clock,pipe] [--clock-ms 500]");
    return 2;
}

var session = args[0];
var sourceNames = "keyboard,clock";
var clockMs = 500;

for (var i = 1; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--sources": sourceNames = args[i + 1]; break;
        case "--clock-ms":
            if (int.TryParse(args[i + 1], out var ms) && ms > 0) clockMs = ms;
            break;
    }
}

using var stage = new TetrisStage(session);

foreach (var name in sourceNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    IInputSource source = name.ToLowerInvariant() switch
    {
        "keyboard" => new KeyboardSource(),
        "pipe" => new PipeSource(session),
        "clock" => new ClockSource(clockMs),
        _ => throw new ArgumentException($"unknown source '{name}'. known: keyboard, clock, pipe"),
    };
    stage.InputSource(source);
}

var srcLabels = string.Join(", ", stage.Sources.Select(s => s.Name));
Console.WriteLine(
    $"TetrisStage: session '{session}', merged sources [{srcLabels}], clock {clockMs}ms. " +
    "Stop with `quit` (keyboard Q/Esc or `TetrisSend {session} quit`) or Ctrl-C.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

stage.Run(cts.Token);

Console.WriteLine(stage.IsGameOver ? "TetrisStage: game over." : "TetrisStage: stopped.");
return 0;
