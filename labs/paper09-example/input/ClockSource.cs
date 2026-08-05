using System;
using System.Threading;

namespace Tetris.Input;

/// <summary>
/// Input source whose medium is the CLOCK. Transport: a sleep loop. Routing:
/// every tick of the wall clock is the single logical command <c>tick</c>
/// (gravity). Autonomous and configurable — the interval is the gravity speed.
/// The clock is just another source feeding the same merge; the automaton does
/// not distinguish a clock-driven tick from a keyboard- or pipe-driven one.
/// </summary>
public sealed class ClockSource : IInputSource
{
    private readonly int intervalMs;

    public ClockSource(int intervalMs) => this.intervalMs = Math.Max(1, intervalMs);

    public string Name => "clock";

    public void Run(Action<string> submit, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (ct.WaitHandle.WaitOne(intervalMs))
            {
                return; // cancelled during the interval
            }

            submit("tick");
        }
    }
}
