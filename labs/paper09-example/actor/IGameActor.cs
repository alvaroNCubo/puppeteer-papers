using System;

namespace Tetris.Acting;

/// <summary>
/// The staging-facing surface of a Tetris game: the verbs a host drives and the
/// one typed read it renders from. It exists because the domain was RE-CUT and
/// there are now two things that offer it — <see cref="TetrisActor"/>, over the
/// single <c>Well</c>, and <see cref="SplitTetrisActor"/>, over the pile role and
/// the piece role — and a host should not have to know which it is holding.
/// <para>
/// This is the cheap side of the line: introducing it cost the original actor one
/// line (the declaration) and each host one line (the variable's type). The
/// re-decomposition itself is a change to the domain, made for reasons of
/// modelling; that a host can be pointed at either cut is a property of the
/// surface, not something either cut had to negotiate.
/// </para>
/// </summary>
public interface IGameActor : IDisposable
{
    /// <summary>Spawns the next piece, chosen by the domain's own selection policy.</summary>
    void SpawnNext();

    /// <summary>
    /// Spawns the piece named by <paramref name="letter"/> (one of
    /// "I", "O", "T", "S", "Z", "J", "L") — the deterministic spawn, for a caller
    /// that has already resolved which piece comes next (a script, a replay, or an
    /// experiment feeding two cuts the same input).
    /// </summary>
    void Spawn(string letter);

    /// <summary>Slides the falling piece one column left (a blocked slide is a no-op).</summary>
    void MoveLeft();

    /// <summary>Slides the falling piece one column right (a blocked slide is a no-op).</summary>
    void MoveRight();

    /// <summary>Rotates the falling piece one step (a blocked rotation is a no-op).</summary>
    void Rotate();

    /// <summary>Advances the falling piece one row; lands it if it cannot descend.</summary>
    void Tick();

    /// <summary>Hard-drops the falling piece to its resting place and lands it.</summary>
    void Drop();

    /// <summary>Drives whatever reactions the staging has wired (frame push, choreography).</summary>
    void RunReactions();

    /// <summary>The current state of the game as one immutable, framework-free value.</summary>
    WellSnapshot Snapshot();
}
