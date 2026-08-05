using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tetris.Acting;

namespace Tetris.Input;

/// <summary>
/// The runner that animates ONE warm <see cref="TetrisActor"/> from a MERGE of
/// <see cref="IInputSource"/>s. Sources are added fluently with
/// <see cref="InputSource"/> — the mirror of the output side's
/// <c>OutputTarget</c>.
/// <para>
/// THE MERGE IS A SERIAL CHANNEL. Every source runs on its own thread and
/// <c>submit</c>s into one thread-safe queue (<see cref="BlockingCollection{T}"/>);
/// the run loop <c>Take</c>s exactly ONE command at a time and applies it to the
/// automaton. This seriality is the whole point: the automaton is a deterministic
/// serial reducer, so there are no races, the journal is a total order, and the
/// game is replayable. Parallelism lives ACROSS actors and at the SOURCE EDGES
/// (each medium read concurrently), never inside the automaton.
/// </para>
/// <para>
/// Two-level routing: (1) each source maps its raw medium signal to a logical
/// command (its own route table); (2) this runner maps the logical command to an
/// actor verb (<see cref="Apply"/>). Symmetric with the web (transport + routing)
/// and with <c>OutputTarget</c> (Bindings route a projection to subscribers).
/// </para>
/// </summary>
public sealed class TetrisStage : IDisposable
{
    private const int Width = 10;
    private const int Height = 20;

    private readonly TetrisActor game;
    private readonly List<IInputSource> sources = new();

    public TetrisStage(string session)
    {
        var journalDir = SessionPaths.For(session);
        var fresh = !System.IO.Directory.Exists(journalDir)
                    || !System.IO.Directory.EnumerateFileSystemEntries(journalDir).GetEnumerator().MoveNext();
        System.IO.Directory.CreateDirectory(journalDir);

        // One warm actor, rehydrated once; every command reuses it. Frames push to
        // the shared per-session frame file the watcher reads.
        var sink = new FrameFileSink(SessionPaths.FrameFile(session));
        game = TetrisActor.Persistent(session, Width, Height, journalDir, sink);

        if (fresh)
        {
            SpawnIfAwaiting();   // first piece
            game.RunReactions(); // opening frame
        }
    }

    /// <summary>Adds an input source to the merge. Fluent — the mirror of OutputTarget.</summary>
    public TetrisStage InputSource(IInputSource source)
    {
        sources.Add(source);
        return this;
    }

    public IReadOnlyList<IInputSource> Sources => sources;

    /// <summary>
    /// Starts every source on its own thread and drains the serial command
    /// channel until a <c>quit</c>, a game over, or <paramref name="externalCt"/>.
    /// </summary>
    public void Run(CancellationToken externalCt)
    {
        // The MERGE: many sources feed this one queue concurrently…
        using var commands = new BlockingCollection<string>(new ConcurrentQueue<string>());
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        var sourceTasks = new List<Task>();
        foreach (var source in sources)
        {
            var s = source;
            sourceTasks.Add(Task.Run(() =>
            {
                try
                {
                    // submit(): the source's only coupling to the merge — enqueue
                    // a logical command. Ignored once the channel is completed.
                    s.Run(cmd => { try { commands.Add(cmd, cts.Token); } catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException) { } }, cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }, cts.Token));
        }

        try
        {
            // …and the automaton processes them ONE AT A TIME — the deterministic
            // serial reducer.
            foreach (var command in commands.GetConsumingEnumerable(cts.Token))
            {
                if (command == "quit")
                {
                    break;
                }

                Apply(command);

                if (game.Snapshot().IsGameOver)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Stop all sources, then let their threads unwind.
            cts.Cancel();
            commands.CompleteAdding();
            try { Task.WaitAll(sourceTasks.ToArray(), TimeSpan.FromSeconds(2)); } catch { }
        }
    }

    // Level-2 routing: logical command -> actor verb. Same orchestration as
    // Tetris/ai (Check-guarded verbs; tick/drop then spawn-if-awaiting; quit is
    // handled by the run loop). Then RunReactions() pushes the frame.
    private void Apply(string command)
    {
        var snapshot = game.Snapshot();
        var active = !snapshot.IsAwaitingPiece && !snapshot.IsGameOver;

        switch (command)
        {
            case "left": if (active) game.MoveLeft(); break;
            case "right": if (active) game.MoveRight(); break;
            case "rotate": if (active) game.Rotate(); break;
            case "tick": if (active) { game.Tick(); SpawnIfAwaiting(); } break;
            case "drop": if (active) { game.Drop(); SpawnIfAwaiting(); } break;
            default: return; // unknown logical command: ignore
        }

        game.RunReactions(); // push the frame

        var after = game.Snapshot();
        Console.WriteLine(
            $"applied: {command} -> type={after.ActiveType ?? "-"} cleared={after.ClearedLines} " +
            $"awaiting={after.IsAwaitingPiece} over={after.IsGameOver}");
    }

    private void SpawnIfAwaiting()
    {
        if (game.Snapshot().IsAwaitingPiece)
        {
            game.SpawnNext();
        }
    }

    public bool IsGameOver => game.Snapshot().IsGameOver;

    public void Dispose() => game.Dispose();
}
