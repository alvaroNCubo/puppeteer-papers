using System;
using System.Threading;

namespace Tetris.Input;

/// <summary>
/// Input source whose medium is the KEYBOARD. Transport: a blocking
/// <see cref="Console.ReadKey(bool)"/> loop. Routing: a fixed keymap (the
/// source's route table) — the same mapping the human console's ApplyKey uses.
/// </summary>
public sealed class KeyboardSource : IInputSource
{
    public string Name => "keyboard";

    public void Run(Action<string> submit, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Poll so cancellation is responsive even though ReadKey blocks.
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(15);
                continue;
            }

            var key = Console.ReadKey(intercept: true).Key;

            // Routing: raw key signal -> logical command. Unmapped keys are
            // dropped (the route table simply has no entry for them).
            var command = key switch
            {
                ConsoleKey.LeftArrow => "left",
                ConsoleKey.RightArrow => "right",
                ConsoleKey.UpArrow => "rotate",
                ConsoleKey.DownArrow => "tick",   // soft drop
                ConsoleKey.Spacebar => "drop",     // hard drop
                ConsoleKey.Q or ConsoleKey.Escape => "quit",
                _ => null,
            };

            if (command is not null)
            {
                submit(command);
                if (command == "quit")
                {
                    return;
                }
            }
        }
    }
}
