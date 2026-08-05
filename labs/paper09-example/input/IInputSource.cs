using System;
using System.Threading;

namespace Tetris.Input;

/// <summary>
/// A named INPUT SOURCE — the input seam, the symmetric sibling of
/// <c>OutputTarget</c>. An actor is animated by a MERGE of input sources; each
/// source does two jobs internally:
/// <list type="number">
/// <item><b>Transport</b> — read its own medium (a keyboard, a named pipe, a
/// clock). The medium is a property of the SOURCE; pull vs push is a property of
/// the destination, never of the command. The logical command — and a
/// <c>print</c> projection on the way out — are medium-agnostic.</item>
/// <item><b>Routing</b> — map a raw medium signal to a LOGICAL command (its own
/// route table). This mirrors the web's transport+URL/verb routing, and the
/// output side's <c>OutputTarget</c> routing of projections to subscribers via
/// Bindings.</item>
/// </list>
/// <para>
/// This is the first of TWO levels of routing. Level 1 is here: raw signal →
/// logical command. Level 2 lives in the runner: logical command → actor verb.
/// </para>
/// <para>
/// A source <see cref="Run"/>s on its own thread until cancelled, calling
/// <c>submit(logicalCommand)</c> once per routed signal. Many sources submit
/// concurrently; the runner's serial channel is the merge point (see
/// <see cref="TetrisStage"/>).
/// </para>
/// </summary>
public interface IInputSource
{
    /// <summary>A short identifier for the source (for logging / the merge HUD).</summary>
    string Name { get; }

    /// <summary>
    /// Reads this source's medium and routes each signal to a logical command,
    /// calling <paramref name="submit"/> per command, until
    /// <paramref name="ct"/> is cancelled. Logical commands are
    /// <c>left | right | rotate | tick | drop | quit</c>.
    /// </summary>
    void Run(Action<string> submit, CancellationToken ct);
}
